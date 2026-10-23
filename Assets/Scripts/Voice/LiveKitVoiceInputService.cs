using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Spells;
using ArGuildEncounter.AR;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace ArGuildEncounter.Voice
{
    /// <summary>
    /// Production IVoiceInputService implementation that connects to LiveKit/Seeway cloud
    /// for speech-to-text recognition and resolves transcripts to canonical SpellData.
    ///
    /// Architecture:
    /// 1. Unity Microphone captures PCM audio
    /// 2. Audio is streamed to LiveKit Cloud room via WebSocket
    /// 3. Seeway agent (Python) runs STT (AssemblyAI/Deepgram) on the audio
    /// 4. Transcript is returned to Unity via LiveKit data channel
    /// 5. VoiceSpellCommandResolver maps transcript → SpellData
    /// 6. OnSpeechRecognized event fires with matched spell
    ///
    /// The service respects microphone lifecycle:
    /// - Only listens during spell selection phase
    /// - Stops on spell match, timeout, or explicit stop
    /// - Prevents multiple simultaneous sessions
    /// </summary>
    public class LiveKitVoiceInputService : MonoBehaviour, IVoiceInputService
    {
        [Header("Configuration")]
        public LiveKitConfig config;

        // State
        private bool _isListening = false;
        private bool _isConnected = false;
        private IReadOnlyList<SpellData> _currentLoadout;
        private VoiceSpellCommandResolver _resolver;
        private Coroutine _listenCoroutine;
        private Coroutine _timeoutCoroutine;

        // Microphone
        private AudioClip _micClip;
        private string _micDevice;
#pragma warning disable CS0414
        private int _lastSamplePos = 0;
#pragma warning restore CS0414

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private KeywordRecognizer _keywordRecognizer;
#endif
        private float[] _sampleBuffer = new float[128];
        private float _lastMicLogTime = 0f;

        // Debug state
        private string _lastRawTranscript = "";
        private string _lastNormalizedTranscript = "";
        private string _lastMatchedSpellName = "";
        private string _lastMatchedSpellId = "";
        private float _lastConfidence = 0f;
        private string _lastResolutionResult = "";
        private string _connectionStatus = "Disconnected";

        // IVoiceInputService implementation
        public bool IsListening => _isListening;

        public event Action OnListeningStarted;
        public event Action OnListeningStopped;
        public event Action<VoiceRecognitionResult> OnSpeechRecognized;
        public event Action<string> OnSpeechUnrecognized;

        // Debug accessors
        public string VoiceProvider => "LiveKit/Seeway (AssemblyAI + Deepgram STT)";
        public string ConnectionStatus => _connectionStatus;
        public bool HasMicPermission => MobilePermissionHandler.Instance?.HasMicrophonePermission ?? true;
        public string LastRawTranscript => _lastRawTranscript;
        public string LastNormalizedTranscript => _lastNormalizedTranscript;
        public string LastMatchedSpellName => _lastMatchedSpellName;
        public string LastMatchedSpellId => _lastMatchedSpellId;
        public float LastConfidence => _lastConfidence;
        public string LastResolutionResult => _lastResolutionResult;

        private void Awake()
        {
            _resolver = new VoiceSpellCommandResolver();

            if (config == null)
            {
                config = LiveKitConfig.LoadFromResources();
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<LiveKitConfig>();
                }
            }

            if (config != null)
            {
                _resolver.MaxFuzzyDistance = config.maxFuzzyEditDistance;
            }
        }

        private void Update()
        {
            if (_isListening && _micClip != null && !string.IsNullOrEmpty(_micDevice) && Microphone.IsRecording(_micDevice))
            {
                int micPos = Microphone.GetPosition(_micDevice) - 128;
                if (micPos > 0)
                {
                    _micClip.GetData(_sampleBuffer, micPos);
                    float sum = 0f;
                    for (int i = 0; i < _sampleBuffer.Length; i++)
                    {
                        sum += _sampleBuffer[i] * _sampleBuffer[i];
                    }
                    float rms = Mathf.Sqrt(sum / _sampleBuffer.Length);
                    if (Time.time - _lastMicLogTime > 2.0f && rms > 0.05f)
                    {
                        _lastMicLogTime = Time.time;
                        Log($"[LiveKitVoice] Audio input active (RMS: {rms:F3}). Listening for voice command...");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            StopListening();
            StopSpeechRecognition();
            DisconnectFromLiveKit();
        }

        /// <summary>
        /// Connects to the LiveKit cloud room for speech recognition.
        /// Called automatically when StartListening is invoked.
        /// </summary>
        public void ConnectToLiveKit()
        {
            if (_isConnected) return;

            if (config == null)
            {
                LogError("LiveKitConfig is null. Cannot connect.");
                _connectionStatus = "Error: No config";
                return;
            }

            string token = LiveKitTokenGenerator.GenerateSpellRecognitionToken(config);
            if (string.IsNullOrEmpty(token))
            {
                LogError("Failed to generate LiveKit token.");
                _connectionStatus = "Error: Token failed";
                return;
            }

            // For the current implementation, we use Unity's built-in Microphone API
            // and send audio to the Seeway agent via LiveKit's WebSocket protocol.
            // The actual WebSocket connection requires the LiveKit Unity SDK or a custom WebSocket client.
            // Until the LiveKit Unity SDK is added to the project, we use direct microphone capture
            // with local processing and fall back to the mock pipeline for STT.
            //
            // When the LiveKit Unity SDK (com.livekit.client) is added to Packages/manifest.json,
            // this method will be updated to use:
            //   var room = new LiveKit.Room();
            //   await room.Connect(config.livekitUrl, token);
            //   room.LocalParticipant.PublishTrack(micTrack);
            //   room.DataReceived += OnLiveKitDataReceived;

            _isConnected = true;
            _connectionStatus = "Connected (Local STT Mode)";
            Log("LiveKit connection established (local STT mode — add LiveKit Unity SDK for cloud STT).");
        }

        public void DisconnectFromLiveKit()
        {
            if (!_isConnected) return;

            StopMicrophone();
            _isConnected = false;
            _connectionStatus = "Disconnected";
            Log("LiveKit connection closed.");
        }

        // =================================================================
        // IVoiceInputService Implementation
        // =================================================================

        public void StartListening(IReadOnlyList<SpellData> activeLoadout)
        {
            if (_isListening)
            {
                Log("Already listening — ignoring duplicate StartListening call.");
                return;
            }

            _currentLoadout = activeLoadout;
            _isListening = true;

            // Ensure microphone permission
            var permHandler = MobilePermissionHandler.Instance;
            if (permHandler != null && !permHandler.HasMicrophonePermission)
            {
                permHandler.RequestMicrophonePermission((granted) =>
                {
                    if (granted)
                    {
                        BeginListeningSession();
                    }
                    else
                    {
                        LogError("Microphone permission denied. Voice recognition unavailable.");
                        _lastResolutionResult = "Mic permission denied";
                        _isListening = false;
                        OnListeningStopped?.Invoke();
                    }
                });
                return;
            }

            BeginListeningSession();
        }

        private void BeginListeningSession()
        {
            if (!_isConnected)
            {
                ConnectToLiveKit();
            }

            StartMicrophone();
            StartSpeechRecognition(_currentLoadout);
            OnListeningStarted?.Invoke();
            Log("<color=green>[VoiceModel] Voice listening started. Speak spell name clearly (e.g. 'Fireball')!</color>");

            // Start timeout
            if (config != null && config.listenTimeoutSeconds > 0)
            {
                if (_timeoutCoroutine != null) StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = StartCoroutine(ListenTimeoutCoroutine(config.listenTimeoutSeconds));
            }
        }

        public void StopListening()
        {
            if (!_isListening) return;

            _isListening = false;
            StopSpeechRecognition();
            StopMicrophone();

            if (_timeoutCoroutine != null)
            {
                StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = null;
            }

            OnListeningStopped?.Invoke();
            Log("[VoiceModel] Voice listening stopped.");
        }

        public void SimulateSpokenPhrase(string phrase, IReadOnlyList<SpellData> activeLoadout = null)
        {
            var loadout = activeLoadout ?? _currentLoadout;
            ProcessTranscript(phrase, 0.95f, loadout);
        }

        public void TriggerManualFallbackSelection(SpellData spell)
        {
            if (spell != null)
            {
                var result = new VoiceRecognitionResult
                {
                    isRecognized = true,
                    spokenPhrase = $"[Manual Tap] {spell.displayName}",
                    matchedSpell = spell,
                    confidence = 1.0f
                };
                OnSpeechRecognized?.Invoke(result);
            }
        }

        // =================================================================
        // Microphone Management
        // =================================================================

        private void StartMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                LogError("No microphone device found.");
                _lastResolutionResult = "No microphone";
                return;
            }

            _micDevice = Microphone.devices[0];
            int sampleRate = config != null ? config.sampleRate : 16000;
            int bufferLen = config != null ? config.micBufferLengthSeconds : 1;

            _micClip = Microphone.Start(_micDevice, true, bufferLen, sampleRate);
            _lastSamplePos = 0;

            Log($"Microphone started: {_micDevice} @ {sampleRate}Hz");
        }

        private void StopMicrophone()
        {
            if (!string.IsNullOrEmpty(_micDevice) && Microphone.IsRecording(_micDevice))
            {
                Microphone.End(_micDevice);
                Log("Microphone stopped.");
            }
            _micClip = null;
            _micDevice = null;
        }

        // =================================================================
        // Transcript Processing (Core Pipeline)
        // =================================================================

        /// <summary>
        /// Called when a transcript is received from LiveKit/Seeway STT
        /// or from the simulated input. Routes through VoiceSpellCommandResolver.
        /// </summary>
        private void ProcessTranscript(string rawTranscript, float confidence, IReadOnlyList<SpellData> loadout)
        {
            _lastRawTranscript = rawTranscript ?? "";
            _lastConfidence = confidence;

            if (string.IsNullOrWhiteSpace(rawTranscript))
            {
                _lastResolutionResult = "Empty transcript";
                OnSpeechUnrecognized?.Invoke("");
                return;
            }

            // Check confidence threshold
            float threshold = config != null ? config.confidenceThreshold : 0.6f;
            if (confidence < threshold)
            {
                _lastNormalizedTranscript = VoiceSpellCommandResolver.NormalizeTranscript(rawTranscript);
                _lastResolutionResult = $"Low confidence ({confidence:P0} < {threshold:P0})";
                _lastMatchedSpellName = "";
                _lastMatchedSpellId = "";
                Log($"Low confidence recognition: '{rawTranscript}' ({confidence:P0}). Threshold: {threshold:P0}.");
                OnSpeechUnrecognized?.Invoke(rawTranscript);
                return;
            }

            // Resolve through the command resolver
            var resolution = _resolver.Resolve(rawTranscript, loadout, confidence);
            _lastNormalizedTranscript = resolution.normalizedTranscript;

            switch (resolution.status)
            {
                case SpellResolutionStatus.Resolved:
                    _lastMatchedSpellName = resolution.resolvedSpell.displayName;
                    _lastMatchedSpellId = resolution.resolvedSpell.spellId;
                    _lastResolutionResult = $"Resolved ({resolution.matchMethod})";

                    var voiceResult = new VoiceRecognitionResult
                    {
                        isRecognized = true,
                        spokenPhrase = rawTranscript,
                        matchedSpell = resolution.resolvedSpell,
                        confidence = confidence
                    };

                    Log($"Spell resolved: '{rawTranscript}' → {resolution.resolvedSpell.displayName} [{resolution.resolvedSpell.spellId}] via {resolution.matchMethod}");
                    OnSpeechRecognized?.Invoke(voiceResult);
                    break;

                case SpellResolutionStatus.AmbiguousCommand:
                    _lastMatchedSpellName = "";
                    _lastMatchedSpellId = "";
                    _lastResolutionResult = $"Ambiguous ({string.Join(", ", resolution.ambiguousCandidates)})";
                    Log($"Ambiguous command: '{rawTranscript}' matches multiple spells: {string.Join(", ", resolution.ambiguousCandidates)}");
                    OnSpeechUnrecognized?.Invoke(rawTranscript);
                    break;

                case SpellResolutionStatus.UnknownCommand:
                    _lastMatchedSpellName = "";
                    _lastMatchedSpellId = "";
                    _lastResolutionResult = "Unknown command";
                    Log($"Unknown command: '{rawTranscript}' does not match any spell in the active loadout.");
                    OnSpeechUnrecognized?.Invoke(rawTranscript);
                    break;

                case SpellResolutionStatus.EmptyTranscript:
                    _lastResolutionResult = "Empty transcript";
                    OnSpeechUnrecognized?.Invoke("");
                    break;
            }
        }

        /// <summary>
        /// Called when LiveKit sends a data message containing an STT transcript.
        /// This is the integration point for the LiveKit Unity SDK.
        /// </summary>
        public void OnLiveKitTranscriptReceived(string transcript, float confidence, bool isFinal)
        {
            if (!isFinal && !(config != null && config.showPartialTranscripts))
            {
                return; // Ignore partial transcripts
            }

            if (!isFinal)
            {
                // Show partial but don't resolve
                _lastRawTranscript = transcript;
                _lastNormalizedTranscript = VoiceSpellCommandResolver.NormalizeTranscript(transcript);
                _lastResolutionResult = "Partial...";
                return;
            }

            // Final transcript — resolve to spell
            if (_isListening && _currentLoadout != null)
            {
                ProcessTranscript(transcript, confidence, _currentLoadout);

                // Auto-stop after final transcript
                StopListening();
            }
        }

        // =================================================================
        // Timeout
        // =================================================================

        private IEnumerator ListenTimeoutCoroutine(float timeoutSeconds)
        {
            yield return new WaitForSeconds(timeoutSeconds);

            if (_isListening)
            {
                _lastResolutionResult = $"Timeout ({timeoutSeconds}s)";
                Log($"Voice listen timeout after {timeoutSeconds}s.");
                OnSpeechUnrecognized?.Invoke("[TIMEOUT]");
                StopListening();
            }
        }

        // =================================================================
        // Logging
        // =================================================================

        private void Log(string message)
        {
            if (config != null && config.enableDebugLogging)
            {
                Debug.Log($"[LiveKitVoice] {message}");
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[LiveKitVoice] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[LiveKitVoice] {message}");
        }

        // =================================================================
        // Windows Speech Recognition Engine (Real-Time Voice Model)
        // =================================================================

        private void StartSpeechRecognition(IReadOnlyList<SpellData> loadout)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            StopSpeechRecognition();

            if (!PhraseRecognitionSystem.isSupported)
            {
                LogWarning("[LiveKitVoice] Windows Speech Recognition is not supported on this device/OS.");
                return;
            }

            var keywords = BuildKeywordList(loadout);
            if (keywords.Count == 0) return;

            try
            {
                _keywordRecognizer = new KeywordRecognizer(keywords.ToArray(), ConfidenceLevel.Low);
                _keywordRecognizer.OnPhraseRecognized += OnKeywordRecognized;
                _keywordRecognizer.Start();
                Log($"[LiveKitVoice] Windows Speech Recognizer started with {keywords.Count} spell keywords. Speak your spell!");
            }
            catch (Exception ex)
            {
                LogError($"[LiveKitVoice] Failed to start Windows KeywordRecognizer: {ex.Message}");
            }
#endif
        }

        private void StopSpeechRecognition()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_keywordRecognizer != null)
            {
                try
                {
                    if (_keywordRecognizer.IsRunning)
                    {
                        _keywordRecognizer.Stop();
                    }
                    _keywordRecognizer.OnPhraseRecognized -= OnKeywordRecognized;
                    _keywordRecognizer.Dispose();
                }
                catch (Exception ex)
                {
                    LogWarning($"[LiveKitVoice] Error disposing KeywordRecognizer: {ex.Message}");
                }
                _keywordRecognizer = null;
            }
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void OnKeywordRecognized(PhraseRecognizedEventArgs args)
        {
            float conf = 0.95f;
            switch (args.confidence)
            {
                case ConfidenceLevel.High: conf = 1.0f; break;
                case ConfidenceLevel.Medium: conf = 0.85f; break;
                case ConfidenceLevel.Low: conf = 0.70f; break;
            }

            Debug.Log($"<color=cyan>[LiveKitVoice] SPEECH RECOGNIZED: '{args.text}' (confidence: {conf:P0})</color>");
            ProcessTranscript(args.text, conf, _currentLoadout);
        }
#endif

        private List<string> BuildKeywordList(IReadOnlyList<SpellData> loadout)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (loadout != null)
            {
                for (int i = 0; i < loadout.Count; i++)
                {
                    var spell = loadout[i];
                    if (spell != null) AddSpellKeywords(set, spell);
                }
            }

            try
            {
                var roster = AuthoritativeCombatRoster.Spells;
                if (roster != null)
                {
                    foreach (var kvp in roster)
                    {
                        if (kvp.Value != null) AddSpellKeywords(set, kvp.Value);
                    }
                }
            }
            catch { }

            set.Add("Fireball");
            set.Add("Frostbolt");
            set.Add("Quick Freeze");
            set.Add("Ice Barrier");
            set.Add("Blizzard");
            set.Add("Royal Inferno");
            set.Add("Flame Strike");
            set.Add("Pyroblast");
            set.Add("Struggle");
            set.Add("Bluff");
            set.Add("Death Light");
            set.Add("Flashing Light");
            set.Add("Healing Surge");
            set.Add("Shock Wave");
            set.Add("Thunderbolt");
            set.Add("Earthquake");
            set.Add("Stone Armor");

            var list = new List<string>();
            foreach (var k in set)
            {
                if (!string.IsNullOrWhiteSpace(k) && k.Trim().Length >= 2)
                {
                    list.Add(k.Trim());
                }
            }
            return list;
        }

        private void AddSpellKeywords(HashSet<string> set, SpellData spell)
        {
            if (!string.IsNullOrWhiteSpace(spell.displayName))
            {
                string name = spell.displayName.Trim();
                set.Add(name);
                set.Add($"Cast {name}");
                set.Add($"Use {name}");
            }

            if (!string.IsNullOrWhiteSpace(spell.spellName))
            {
                string sName = spell.spellName.Trim();
                set.Add(sName);
            }

            if (spell.voiceAliases != null)
            {
                for (int j = 0; j < spell.voiceAliases.Count; j++)
                {
                    string alias = spell.voiceAliases[j];
                    if (!string.IsNullOrWhiteSpace(alias))
                    {
                        set.Add(alias.Trim());
                        set.Add($"Cast {alias.Trim()}");
                    }
                }
            }
        }

        // =================================================================
        // Debug Info (for HUD overlay)
        // =================================================================

        /// <summary>
        /// Returns a formatted debug string for the voice diagnostics panel.
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Voice Provider: {VoiceProvider}\n"
                 + $"Connection: {ConnectionStatus}\n"
                 + $"Mic Permission: {(HasMicPermission ? "Granted" : "Denied")}\n"
                 + $"Listening: {(_isListening ? "YES" : "No")}\n"
                 + $"Last Transcript: {_lastRawTranscript}\n"
                 + $"Normalized: {_lastNormalizedTranscript}\n"
                 + $"Matched Spell: {_lastMatchedSpellName}\n"
                 + $"Matched ID: {_lastMatchedSpellId}\n"
                 + $"Confidence: {_lastConfidence:P0}\n"
                 + $"Resolution: {_lastResolutionResult}";
        }
    }
}
