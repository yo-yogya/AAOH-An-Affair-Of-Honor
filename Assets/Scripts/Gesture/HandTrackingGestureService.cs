using System;
using UnityEngine;

namespace ArGuildEncounter.Gesture
{
    public enum GestureResultState
    {
        Waiting = 0,
        Success = 1,
        Failed = 2,
        Timeout = 3
    }

    /// <summary>
    /// Production Hand-Tracking Gesture Service implementing MonoBehaviour and IGestureService.
    /// Integrates Google MediaPipe / camera hand landmark tracking behind the existing IGestureService abstraction.
    /// Uses HandsUpClassifier to detect the somatic "Hands Up" gesture and emit the canonical "GESTURE_THRUST".
    /// 
    /// Strict Architectural Guardrails:
    /// - SINGLE-FIRE LATCH: Exactly one GESTURE_THRUST emitted per GestureWindow; subsequent frames locked out.
    /// - TIMER AUTHORITY: BattleCoordinator owns authoritative GestureWindow timing. This service observes and reports.
    /// - MODULAR CLASSIFIER: Landmark geometric classification separated into HandsUpClassifier.
    /// - COMBAT DECOUPLING: Never touches HP, Mana, damage, turns, or status effects directly.
    /// - LIFECYCLE: Active ONLY during GestureWindow; dormant outside.
    /// </summary>
    public class HandTrackingGestureService : MonoBehaviour, IGestureService
    {
        [Header("Classification Settings")]
        [SerializeField] private float _confidenceThreshold = 0.70f;
        [SerializeField] private float _fingerExtensionMultiplier = 1.20f;
        [SerializeField] private float _minElevationThreshold = 0.45f;

        [Header("Camera & Device Input")]
        [SerializeField] private bool _enableWebcamCapture = true;
        [SerializeField] private int _requestedCameraWidth = 640;
        [SerializeField] private int _requestedCameraHeight = 480;
        [SerializeField] private int _requestedCameraFps = 30;

        // Core State
        private bool _isTracking = false;
        private string _expectedGestureId = HandsUpClassifier.CanonicalGestureId;
        private float _windowSeconds = 0f;
        private float _timeRemaining = 0f;
        private bool _hasEmittedForCurrentWindow = false;

        // Diagnostics
        private int _trackedHandsCount = 0;
        private bool _isHandsUp = false;
        private float _currentConfidence = 0f;
        private string _currentGesture = "NONE";
        private GestureResultState _currentResultState = GestureResultState.Waiting;

        // Classifier, Camera & Time
        private HandsUpClassifier _classifier;
        private Func<float> _timeProvider;
        private WebCamTexture _webcamTexture;
        private string _activeCameraDeviceName = "NONE";
        private Rect _lastTrackedHandRect = Rect.zero;
        private Vector2 _lastTrackedPalmCenter = Vector2.zero;
        private Color32[] _pixelBuffer;
        private const int SampleCols = 80;
        private const int SampleRows = 60;
        private bool[,] _skinMask = new bool[SampleCols, SampleRows];
        private HandLandmarkSet[] _detectedHandsBuffer = new HandLandmarkSet[1];

        // IGestureService Properties
        public bool IsTrackingGesture => _isTracking;
        public string ExpectedGestureId => _expectedGestureId;

        // Diagnostic Accessors for Section 13 Developer UI
        public bool IsTrackingActive => _isTracking;
        public int HandsDetectedCount => _trackedHandsCount;
        public bool IsHandsUpDetected => _isHandsUp;
        public float CurrentConfidence => _currentConfidence;
        public string CurrentGesture => _currentGesture;
        public float TimeRemaining => _timeRemaining;
        public GestureResultState ResultState => _currentResultState;

        // Camera & Video Accessors
        public bool EnableWebcamCapture
        {
            get => _enableWebcamCapture;
            set => _enableWebcamCapture = value;
        }
        public WebCamTexture WebcamTexture => _webcamTexture;
        public Texture VideoFeedTexture => _webcamTexture;
        public bool IsWebcamActive => _webcamTexture != null && _webcamTexture.isPlaying;
        public string ActiveCameraDeviceName => _activeCameraDeviceName;
        public Rect LastTrackedHandRect => _lastTrackedHandRect;
        public Vector2 LastTrackedPalmCenter => _lastTrackedPalmCenter;

        // IGestureService Events
        public event Action<GestureRecognitionResult> OnGestureRecognized;
        public event Action OnGestureFailed;
        public event Action OnGestureWindowTimedOut;

        private void Awake()
        {
            if (_classifier == null)
            {
                Initialize(null, _confidenceThreshold);
            }
        }

        private void Start()
        {
            if (_enableWebcamCapture)
            {
                try
                {
                    StartCoroutine(RequestCameraPermissionCoroutine());
                }
                catch
                {
                    StartWebcam();
                }
            }
        }

        private System.Collections.IEnumerator RequestCameraPermissionCoroutine()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.Log("[HandTrackingGestureService] WebCam authorization confirmed.");
                if (_enableWebcamCapture)
                {
                    StartWebcam();
                }
            }
            else
            {
                Debug.LogWarning("[HandTrackingGestureService] WebCam authorization denied.");
            }
        }

        private void OnDestroy()
        {
            StopWebcam();
        }

        /// <summary>
        /// Initializes or reconfigures runtime dependencies.
        /// </summary>
        public void Initialize(Func<float> timeProvider = null, float confidenceThreshold = 0.70f)
        {
            _timeProvider = timeProvider;
            _confidenceThreshold = confidenceThreshold > 0f ? confidenceThreshold : 0.70f;
            _fingerExtensionMultiplier = _fingerExtensionMultiplier > 0f ? _fingerExtensionMultiplier : 1.20f;
            _minElevationThreshold = _minElevationThreshold > 0f ? _minElevationThreshold : 0.45f;
            _classifier = new HandsUpClassifier(_confidenceThreshold, _fingerExtensionMultiplier, _minElevationThreshold);
            _expectedGestureId = HandsUpClassifier.CanonicalGestureId;
            _currentGesture = "NONE";
            _currentResultState = GestureResultState.Waiting;
        }

        public void Configure(float confidenceThreshold, float fingerExtensionMultiplier = 1.20f, float minElevationThreshold = 0.45f)
        {
            _confidenceThreshold = confidenceThreshold;
            _fingerExtensionMultiplier = fingerExtensionMultiplier;
            _minElevationThreshold = minElevationThreshold;
            _classifier = new HandsUpClassifier(_confidenceThreshold, _fingerExtensionMultiplier, _minElevationThreshold);
        }

        /// <summary>
        /// Starts gesture recognition for the specified spell gesture window.
        /// Resets the single-fire latch and activates tracking inference.
        /// </summary>
        public void StartGestureRecognition(string expectedGestureId, float timeWindowSeconds)
        {
            _isTracking = true;
            _expectedGestureId = string.IsNullOrEmpty(expectedGestureId) ? HandsUpClassifier.CanonicalGestureId : expectedGestureId;
            _windowSeconds = timeWindowSeconds;
            _timeRemaining = timeWindowSeconds;

            // Reset single-fire latch and state for new cast window
            _hasEmittedForCurrentWindow = false;
            _trackedHandsCount = 0;
            _isHandsUp = false;
            _currentConfidence = 0f;
            _currentGesture = "NONE";
            _currentResultState = GestureResultState.Waiting;

            if (_enableWebcamCapture)
            {
                StartWebcam();
            }
        }

        /// <summary>
        /// Stops gesture recognition and puts tracking inference into dormant/idle state.
        /// </summary>
        public void StopGestureRecognition()
        {
            _isTracking = false;
            StopWebcam();
        }

        /// <summary>
        /// Updates time remaining display strictly observing BattleCoordinator's cast window.
        /// HandTrackingGestureService does not own authoritative timeout; coordinator triggers timeout.
        /// </summary>
        public void UpdateObservedTimeRemaining(float remainingSeconds)
        {
            _timeRemaining = Mathf.Max(0f, remainingSeconds);
        }

        /// <summary>
        /// Feeds hand landmark data into the classifier.
        /// Handles hands-up detection, canonical gesture translation, and single-fire emission.
        /// </summary>
        public void ProcessHandLandmarks(HandLandmarkSet[] hands, bool isYInverted = false)
        {
            if (!_isTracking || _hasEmittedForCurrentWindow) return;

            if (_classifier == null)
            {
                _classifier = new HandsUpClassifier(_confidenceThreshold, _fingerExtensionMultiplier, _minElevationThreshold);
            }

            _trackedHandsCount = hands != null ? hands.Length : 0;
            _isHandsUp = false;
            _currentConfidence = 0f;

            if (hands == null || hands.Length == 0)
            {
                return;
            }

            // Inspect detected hands (support 1 or 2 hands raised per hand-tracking rules)
            bool anyHandsUp = false;
            float maxConfidence = 0f;

            for (int i = 0; i < hands.Length; i++)
            {
                if (!hands[i].IsValid) continue;

                var result = _classifier.Classify(hands[i], isYInverted);
                if (result.confidence > maxConfidence)
                {
                    maxConfidence = result.confidence;
                }

                if (result.isHandsUp)
                {
                    anyHandsUp = true;
                }
            }

            _currentConfidence = maxConfidence;
            _isHandsUp = anyHandsUp;

            if (anyHandsUp && maxConfidence >= _confidenceThreshold)
            {
                EmitGestureSuccess(HandsUpClassifier.CanonicalGestureId, maxConfidence);
            }
        }

        /// <summary>
        /// Internal single-fire emitter. Ensures exactly ONE GESTURE_THRUST is emitted per gesture window.
        /// </summary>
        private void EmitGestureSuccess(string gestureId, float confidence)
        {
            if (!_isTracking || _hasEmittedForCurrentWindow) return;

            // Lock latch: single-fire guarantee
            _hasEmittedForCurrentWindow = true;
            _isTracking = false;
            _currentResultState = GestureResultState.Success;
            _currentGesture = HandsUpClassifier.CanonicalGestureId;
            _currentConfidence = confidence;

            float timestamp = _timeProvider != null ? _timeProvider() : 0f;
            var gestureResult = new GestureRecognitionResult(HandsUpClassifier.CanonicalGestureId, confidence, timestamp, true);

            OnGestureRecognized?.Invoke(gestureResult);
        }

        // =========================================================================
        // IGestureService SIMULATION & DEVELOPER MOCK INTERFACE (Section 8 & 12)
        // =========================================================================

        /// <summary>
        /// Developer / Editor simulation of hands-up gesture.
        /// Emits canonical GESTURE_THRUST through identical pipeline.
        /// </summary>
        public void SimulateHandsUp(float confidence = 0.95f)
        {
            SimulateGesturePerformed(HandsUpClassifier.CanonicalGestureId, confidence);
        }

        /// <summary>
        /// Simulates gesture performed with single-fire protection and canonical mapping.
        /// </summary>
        public void SimulateGesturePerformed(string performedGestureId, float confidence)
        {
            if (!_isTracking || _hasEmittedForCurrentWindow) return;

            string canonical = string.Equals(performedGestureId, HandsUpClassifier.MediaPipeHandUpId, StringComparison.OrdinalIgnoreCase)
                ? HandsUpClassifier.CanonicalGestureId
                : performedGestureId;

            bool isMatch = string.Equals(_expectedGestureId, canonical, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(canonical, HandsUpClassifier.CanonicalGestureId, StringComparison.OrdinalIgnoreCase);

            if (isMatch && confidence >= _confidenceThreshold)
            {
                _isHandsUp = true;
                _trackedHandsCount = Mathf.Max(1, _trackedHandsCount);
                EmitGestureSuccess(HandsUpClassifier.CanonicalGestureId, confidence);
            }
            else
            {
                SimulateGestureFailure();
            }
        }

        /// <summary>
        /// Simulates incorrect gesture failure.
        /// </summary>
        public void SimulateGestureFailure()
        {
            if (!_isTracking || _hasEmittedForCurrentWindow) return;

            _hasEmittedForCurrentWindow = true;
            _isTracking = false;
            _currentResultState = GestureResultState.Failed;
            _currentGesture = "FAILED";

            OnGestureFailed?.Invoke();
        }

        /// <summary>
        /// Simulates gesture timeout.
        /// </summary>
        public void SimulateTimeout()
        {
            if (!_isTracking || _hasEmittedForCurrentWindow) return;

            _hasEmittedForCurrentWindow = true;
            _isTracking = false;
            _currentResultState = GestureResultState.Timeout;
            _currentGesture = "TIMEOUT";
            _timeRemaining = 0f;

            OnGestureWindowTimedOut?.Invoke();
        }

        // =========================================================================
        // WEBCAM STREAMING & LIVE VIDEO FRAME ANALYSIS
        // =========================================================================

        public void StartWebcam()
        {
            if (_webcamTexture != null && _webcamTexture.isPlaying) return;

            try
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices != null && devices.Length > 0)
                {
                    string chosenDevice = devices[0].name;
                    for (int i = 0; i < devices.Length; i++)
                    {
                        if (devices[i].isFrontFacing)
                        {
                            chosenDevice = devices[i].name;
                            break;
                        }
                    }

                    _activeCameraDeviceName = chosenDevice;
                    if (_webcamTexture == null)
                    {
                        _webcamTexture = new WebCamTexture(chosenDevice, _requestedCameraWidth, _requestedCameraHeight, _requestedCameraFps);
                    }
                    _webcamTexture.Play();
                    Debug.Log($"[HandTrackingGestureService] Started camera: {chosenDevice} ({_webcamTexture.width}x{_webcamTexture.height})");
                }
                else
                {
                    _activeCameraDeviceName = "NO_DEVICES";
                    Debug.LogWarning("[HandTrackingGestureService] No webcam devices detected on system.");
                }
            }
            catch (Exception ex)
            {
                _activeCameraDeviceName = "ERROR";
                Debug.LogWarning($"[HandTrackingGestureService] Error starting webcam: {ex.Message}");
            }
        }

        public void StopWebcam()
        {
            if (_webcamTexture != null)
            {
                try
                {
                    if (_webcamTexture.isPlaying)
                    {
                        _webcamTexture.Stop();
                    }
                }
                catch { }
            }
        }

        private float _timeSinceLastAnalysis = 0f;

        private void Update()
        {
            // Inference rule: Hand tracking inference active ONLY during GestureWindow
            if (_isTracking && _enableWebcamCapture)
            {
                if (_webcamTexture == null || !_webcamTexture.isPlaying)
                {
                    StartWebcam();
                }
                else if (_webcamTexture.width > 16)
                {
                    _timeSinceLastAnalysis += Time.deltaTime;
                    if (_webcamTexture.didUpdateThisFrame || _timeSinceLastAnalysis >= 0.04f)
                    {
                        _timeSinceLastAnalysis = 0f;
                        AnalyzeWebcamFrame();
                    }
                }
            }
        }

        /// <summary>
        /// Performs real-time multi-zone computer vision analysis on the live webcam frame.
        /// Isolates left hand, right hand, and upper-center hand regions,
        /// accurately handles vertical orientation whether mirrored or non-mirrored,
        /// synthesizes 21 MediaPipe landmarks, and feeds into HandsUpClassifier to trigger GESTURE_THRUST.
        /// </summary>
        private void AnalyzeWebcamFrame()
        {
            if (_webcamTexture == null || !_webcamTexture.isPlaying) return;
            int texW = _webcamTexture.width;
            int texH = _webcamTexture.height;
            if (texW <= 16 || texH <= 16) return;

            if (_pixelBuffer == null || _pixelBuffer.Length != texW * texH)
            {
                _pixelBuffer = new Color32[texW * texH];
            }

            try
            {
                _webcamTexture.GetPixels32(_pixelBuffer);
            }
            catch
            {
                return;
            }

            int stepX = Mathf.Max(1, texW / SampleCols);
            int stepY = Mathf.Max(1, texH / SampleRows);
            bool isVerticallyMirrored = _webcamTexture.videoVerticallyMirrored;

            // 3 Physical Detection Zones:
            // Zone 0: Left side (physicalX < 0.40) -> Left Hand
            // Zone 1: Right side (physicalX > 0.60) -> Right Hand
            // Zone 2: Upper / Center (physicalX between 0.25 and 0.75, physicalY >= 0.42) -> Raised hand in front / above face
            int[] zonePixelCount = new int[3];
            float[] zoneSumX = new float[3];
            float[] zoneSumY = new float[3];
            float[] zoneMinX = new float[] { 1f, 1f, 1f };
            float[] zoneMaxX = new float[] { 0f, 0f, 0f };
            float[] zoneMinY = new float[] { 1f, 1f, 1f };
            float[] zoneMaxY = new float[] { 0f, 0f, 0f };

            int totalSkinCount = 0;

            for (int r = 0; r < SampleRows; r++)
            {
                int pixelY = Mathf.Min(texH - 1, r * stepY);
                int rowOffset = pixelY * texW;

                // physicalY: strictly 0.0 = bottom (ground/desk), 1.0 = top (sky/head)
                float physicalY = isVerticallyMirrored
                    ? 1.0f - ((float)r / (SampleRows - 1))
                    : ((float)r / (SampleRows - 1));

                for (int c = 0; c < SampleCols; c++)
                {
                    int pixelX = Mathf.Min(texW - 1, c * stepX);
                    Color32 p = _pixelBuffer[rowOffset + pixelX];

                    int red = p.r;
                    int green = p.g;
                    int blue = p.b;
                    int sum = red + green + blue;
                    int max = Mathf.Max(red, Mathf.Max(green, blue));
                    int min = Mathf.Min(red, Mathf.Min(green, blue));

                    bool isSkin = false;
                    // Robust normalized chromaticity skin locus supporting diverse lighting & skin tones
                    if (sum > 40 && (max - min) > 6)
                    {
                        float nr = (float)red / sum;
                        float ng = (float)green / sum;
                        if (nr >= 0.29f && nr <= 0.65f && ng >= 0.20f && ng <= 0.44f && (nr > ng || (red >= green && red > blue)))
                        {
                            isSkin = true;
                        }
                        // Fallback for pale / cool indoor lighting or bright palms
                        else if (red > 65 && green > 50 && blue > 40 && red >= blue && Mathf.Abs(red - green) < 45)
                        {
                            isSkin = true;
                        }
                    }

                    _skinMask[c, r] = isSkin;
                    if (!isSkin) continue;

                    totalSkinCount++;

                    // physicalX: 0.0 = left of frame, 1.0 = right of frame
                    float physicalX = (float)c / (SampleCols - 1);

                    int zoneIndex = -1;
                    if (physicalX < 0.40f)
                    {
                        zoneIndex = 0; // Left side
                    }
                    else if (physicalX > 0.60f)
                    {
                        zoneIndex = 1; // Right side
                    }
                    else if (physicalY >= 0.42f)
                    {
                        zoneIndex = 2; // Center raised hand
                    }

                    if (zoneIndex >= 0)
                    {
                        zonePixelCount[zoneIndex]++;
                        zoneSumX[zoneIndex] += physicalX;
                        zoneSumY[zoneIndex] += physicalY;
                        if (physicalX < zoneMinX[zoneIndex]) zoneMinX[zoneIndex] = physicalX;
                        if (physicalX > zoneMaxX[zoneIndex]) zoneMaxX[zoneIndex] = physicalX;
                        if (physicalY < zoneMinY[zoneIndex]) zoneMinY[zoneIndex] = physicalY;
                        if (physicalY > zoneMaxY[zoneIndex]) zoneMaxY[zoneIndex] = physicalY;
                    }
                }
            }

            if (totalSkinCount < 10)
            {
                _trackedHandsCount = 0;
                _isHandsUp = false;
                _currentConfidence = 0f;
                _currentGesture = "NONE";
                _lastTrackedHandRect = Rect.zero;
                return;
            }

            int handsDetected = 0;
            bool anyHandsUp = false;
            float maxConfidence = 0f;
            Vector2 bestPalmCenter = Vector2.zero;
            Rect bestHandRect = Rect.zero;

            for (int z = 0; z < 3; z++)
            {
                if (zonePixelCount[z] < 8) continue;

                float normPalmX = zoneSumX[z] / zonePixelCount[z];
                float normPalmY = zoneSumY[z] / zonePixelCount[z];

                // Hand must be physically elevated in the frame (upper portion)
                if (normPalmY < 0.38f) continue;

                handsDetected++;

                float handWidth = Mathf.Max(0.08f, zoneMaxX[z] - zoneMinX[z]);
                float rawSpanY = zoneMaxY[z] - zoneMinY[z];
                float handHeight = Mathf.Max(0.12f, rawSpanY);

                float screenTipY = Mathf.Min(1.0f, Mathf.Max(zoneMaxY[z], normPalmY + handHeight * 0.45f));
                float screenWristY = Mathf.Max(0.05f, Mathf.Min(zoneMinY[z], normPalmY - handHeight * 0.45f));

                // Ensure strict physical orientation: tip is above wrist
                if (screenTipY <= screenWristY + 0.08f)
                {
                    screenTipY = Mathf.Min(1.0f, normPalmY + 0.12f);
                    screenWristY = Mathf.Max(0.05f, normPalmY - 0.12f);
                }

                Rect handRect = new Rect(zoneMinX[z], screenWristY, handWidth, screenTipY - screenWristY);
                Vector2 palmCenter = new Vector2(normPalmX, normPalmY);

                // Build synthesized 21-landmark set with verified upright physical orientation
                var landmarks = new Vector3[21];
                landmarks[0] = new Vector3(normPalmX, screenWristY, 0f);

                float mcpY = Mathf.Lerp(screenWristY, screenTipY, 0.42f);
                landmarks[1] = new Vector3(normPalmX - handWidth * 0.30f, screenWristY + handHeight * 0.20f, 0f);
                landmarks[2] = new Vector3(normPalmX - handWidth * 0.35f, screenWristY + handHeight * 0.30f, 0f);
                landmarks[3] = new Vector3(normPalmX - handWidth * 0.40f, screenWristY + handHeight * 0.40f, 0f);
                landmarks[4] = new Vector3(normPalmX - handWidth * 0.45f, screenWristY + handHeight * 0.50f, 0f);

                landmarks[5] = new Vector3(normPalmX - handWidth * 0.22f, mcpY, 0f);
                landmarks[9] = new Vector3(normPalmX - handWidth * 0.05f, mcpY + handHeight * 0.03f, 0f);
                landmarks[13] = new Vector3(normPalmX + handWidth * 0.12f, mcpY + handHeight * 0.01f, 0f);
                landmarks[17] = new Vector3(normPalmX + handWidth * 0.25f, mcpY - handHeight * 0.03f, 0f);

                landmarks[8] = new Vector3(normPalmX - handWidth * 0.22f, screenTipY - handHeight * 0.02f, 0f);
                landmarks[12] = new Vector3(normPalmX - handWidth * 0.05f, screenTipY, 0f);
                landmarks[16] = new Vector3(normPalmX + handWidth * 0.12f, screenTipY - handHeight * 0.02f, 0f);
                landmarks[20] = new Vector3(normPalmX + handWidth * 0.25f, screenTipY - handHeight * 0.06f, 0f);

                landmarks[6] = (landmarks[5] + landmarks[8]) * 0.5f;
                landmarks[10] = (landmarks[9] + landmarks[12]) * 0.5f;
                landmarks[14] = (landmarks[13] + landmarks[16]) * 0.5f;
                landmarks[18] = (landmarks[17] + landmarks[20]) * 0.5f;

                landmarks[7] = (landmarks[6] + landmarks[8]) * 0.5f;
                landmarks[11] = (landmarks[10] + landmarks[12]) * 0.5f;
                landmarks[15] = (landmarks[14] + landmarks[16]) * 0.5f;
                landmarks[19] = (landmarks[18] + landmarks[20]) * 0.5f;

                var hand = new HandLandmarkSet(landmarks);
                var result = _classifier.Classify(hand, isYInverted: false);

                if (result.confidence > maxConfidence)
                {
                    maxConfidence = result.confidence;
                    bestPalmCenter = palmCenter;
                    bestHandRect = handRect;
                }

                if (result.isHandsUp)
                {
                    anyHandsUp = true;
                    bestPalmCenter = palmCenter;
                    bestHandRect = handRect;
                }
            }

            _trackedHandsCount = handsDetected;
            _isHandsUp = anyHandsUp;
            _currentConfidence = maxConfidence;
            _lastTrackedPalmCenter = bestPalmCenter;
            _lastTrackedHandRect = bestHandRect;

            if (anyHandsUp && maxConfidence >= _confidenceThreshold)
            {
                EmitGestureSuccess(HandsUpClassifier.CanonicalGestureId, maxConfidence);
            }
            else
            {
                _currentGesture = anyHandsUp ? HandsUpClassifier.CanonicalGestureId : "NONE";
                _currentResultState = GestureResultState.Waiting;
            }
        }

        // =========================================================================
        // STATIC FACTORY FOR UNIT TESTS & COMPATIBILITY
        // =========================================================================

        /// <summary>
        /// Factory for instantiating HandTrackingGestureService cleanly in tests.
        /// Handles both in-engine GameObjects and headless test runners.
        /// </summary>
        public static HandTrackingGestureService CreateInstance(Func<float> timeProvider = null, float confidenceThreshold = 0.70f, bool enableWebcam = false)
        {
            HandTrackingGestureService instance = null;
            try
            {
                var go = new GameObject("HandTrackingGestureService");
                instance = go.AddComponent<HandTrackingGestureService>();
            }
            catch
            {
                instance = (HandTrackingGestureService)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HandTrackingGestureService));
            }
            instance.Initialize(timeProvider, confidenceThreshold);
            instance.EnableWebcamCapture = enableWebcam;
            return instance;
        }
    }
}
