using System;
using UnityEngine;

namespace ArGuildEncounter.Gesture
{
    /// <summary>
    /// Data structure for a 21-point hand landmark set matching Google MediaPipe HandLandmarker.
    /// Landmark 0: Wrist
    /// Landmarks 1-4: Thumb (CMC, MCP, IP, Tip)
    /// Landmarks 5-8: Index (MCP, PIP, DIP, Tip)
    /// Landmarks 9-12: Middle (MCP, PIP, DIP, Tip)
    /// Landmarks 13-16: Ring (MCP, PIP, DIP, Tip)
    /// Landmarks 17-20: Pinky (MCP, PIP, DIP, Tip)
    /// </summary>
    [System.Serializable]
    public struct HandLandmarkSet
    {
        public Vector3[] landmarks; // 21 points in normalized [0..1] space
        public float trackingConfidence;

        public HandLandmarkSet(Vector3[] points, float confidence = 1.0f)
        {
            landmarks = points;
            trackingConfidence = confidence;
        }

        public bool IsValid => landmarks != null && landmarks.Length >= 21;

        public Vector3 Wrist => landmarks[0];
        public Vector3 ThumbTip => landmarks[4];
        public Vector3 IndexPip => landmarks[6];
        public Vector3 IndexTip => landmarks[8];
        public Vector3 MiddlePip => landmarks[10];
        public Vector3 MiddleTip => landmarks[12];
        public Vector3 RingPip => landmarks[14];
        public Vector3 RingTip => landmarks[16];
        public Vector3 PinkyPip => landmarks[18];
        public Vector3 PinkyTip => landmarks[20];
    }

    /// <summary>
    /// Structured result produced by the HandsUpClassifier.
    /// </summary>
    public struct HandsUpClassificationResult
    {
        public bool isHandsUp;
        public float confidence;
        public string rawGestureId;
        public string canonicalGestureId;
        public int extendedFingersCount;
        public bool isOpenPalm;
        public bool isHandElevated;
        public string debugDetails;

        public HandsUpClassificationResult(
            bool isHandsUp,
            float confidence,
            string rawGestureId,
            string canonicalGestureId,
            int extendedFingersCount,
            bool isOpenPalm,
            bool isHandElevated,
            string debugDetails)
        {
            this.isHandsUp = isHandsUp;
            this.confidence = confidence;
            this.rawGestureId = rawGestureId;
            this.canonicalGestureId = canonicalGestureId;
            this.extendedFingersCount = extendedFingersCount;
            this.isOpenPalm = isOpenPalm;
            this.isHandElevated = isHandElevated;
            this.debugDetails = debugDetails;
        }
    }

    /// <summary>
    /// Modular, camera-agnostic geometric classifier for the "Hands Up" somatic gesture.
    /// Implements the landmark rules established in hand-tracking/its_giving_v2.py:
    /// - Palm center = mean of landmarks [0, 5, 9, 13, 17]
    /// - Finger extension: dist(wrist, tip) > 1.2 * dist(wrist, pip) for Index, Middle, Ring, Pinky
    /// - Open palm: >= 3 extended fingers
    /// - Hand Up: Open palm, fingertips pointing up (tip.y > wrist.y in viewport), palm elevated in upper frame.
    /// Translates raw classification "hand_up" to canonical Unity gesture "GESTURE_THRUST".
    /// </summary>
    public class HandsUpClassifier
    {
        public const string CanonicalGestureId = "GESTURE_THRUST";
        public const string MediaPipeHandUpId = "hand_up";

        private readonly float _confidenceThreshold;
        private readonly float _fingerExtensionMultiplier;
        private readonly float _minElevationThreshold;

        /// <summary>
        /// Initializes classifier with configurable thresholds.
        /// </summary>
        /// <param name="confidenceThreshold">Minimum acceptable confidence (default 0.70f / 70%)</param>
        /// <param name="fingerExtensionMultiplier">Ratio of tip-to-wrist vs pip-to-wrist distance (default 1.20f per its_giving_v2.py)</param>
        /// <param name="minElevationThreshold">Minimum normalized Y elevation for hand elevation (default 0.45f)</param>
        public HandsUpClassifier(
            float confidenceThreshold = 0.70f,
            float fingerExtensionMultiplier = 1.20f,
            float minElevationThreshold = 0.45f)
        {
            _confidenceThreshold = confidenceThreshold;
            _fingerExtensionMultiplier = fingerExtensionMultiplier;
            _minElevationThreshold = minElevationThreshold;
        }

        /// <summary>
        /// Classifies a set of 21 hand landmarks.
        /// Coordinates are assumed in normalized viewport coordinates:
        /// X: 0 (left) to 1 (right)
        /// Y: 0 (bottom) to 1 (top of frame)
        /// If input coordinates have Y: 0 at top (e.g. OpenCV / raw image), set isYInverted = true.
        /// </summary>
        public HandsUpClassificationResult Classify(HandLandmarkSet hand, bool isYInverted = false)
        {
            if (!hand.IsValid)
            {
                return new HandsUpClassificationResult(
                    isHandsUp: false,
                    confidence: 0f,
                    rawGestureId: "none",
                    canonicalGestureId: "",
                    extendedFingersCount: 0,
                    isOpenPalm: false,
                    isHandElevated: false,
                    debugDetails: "Invalid or insufficient landmarks (<21)");
            }

            Vector3[] pts = (Vector3[])hand.landmarks.Clone();
            if (isYInverted)
            {
                for (int i = 0; i < pts.Length; i++)
                {
                    pts[i].y = 1.0f - pts[i].y;
                }
            }

            Vector3 wrist = pts[0];

            // 1. Compute Palm Center = mean of [0, 5, 9, 13, 17] (Wrist + 4 MCP joints) per its_giving_v2.py
            Vector3 palmCenter = (pts[0] + pts[5] + pts[9] + pts[13] + pts[17]) / 5f;

            // 2. Compute Extended Fingers for Index(8/6), Middle(12/10), Ring(16/14), Pinky(20/18)
            // Rule: dist(wrist, tip) > 1.2 * dist(wrist, pip)
            int extendedCount = 0;
            int[] tipIndices = { 8, 12, 16, 20 };
            int[] pipIndices = { 6, 10, 14, 18 };

            for (int i = 0; i < 4; i++)
            {
                int tipIdx = tipIndices[i];
                int pipIdx = pipIndices[i];

                float distTip = Vector3.Distance(wrist, pts[tipIdx]);
                float distPip = Vector3.Distance(wrist, pts[pipIdx]);

                if (distTip > distPip * _fingerExtensionMultiplier)
                {
                    extendedCount++;
                }
            }

            // 3. Open Hand check: sum(extended) >= 3
            bool isOpenPalm = extendedCount >= 3;

            // 4. Upward Orientation: Fingertips pointing up relative to wrist
            // In viewport coordinates (Y=1 top), tips must be above wrist (tip.y > wrist.y)
            bool isOrientedUp = (pts[8].y > wrist.y) && (pts[12].y > wrist.y);

            // 5. Elevation: Palm is in the upper portion of the frame
            bool isHandElevated = palmCenter.y >= _minElevationThreshold;

            // 6. Calculate Confidence
            // Higher elevation and all 4 extended fingers scale confidence up to 0.98f
            float elevationBonus = Mathf.Clamp01((palmCenter.y - _minElevationThreshold) / 0.40f) * 0.15f;
            float fingersBonus = (extendedCount / 4f) * 0.15f;
            float rawConfidence = isOpenPalm && isOrientedUp && isHandElevated
                ? Mathf.Clamp01(0.70f + elevationBonus + fingersBonus)
                : Mathf.Clamp01((extendedCount / 4f) * 0.40f);

            float totalConfidence = rawConfidence * Mathf.Clamp01(hand.trackingConfidence);
            bool isSuccess = isOpenPalm && isOrientedUp && isHandElevated && (totalConfidence >= _confidenceThreshold);

            string rawGesture = isSuccess ? MediaPipeHandUpId : "none";
            string canonicalGesture = isSuccess ? CanonicalGestureId : "";

            string details = $"ExtFingers={extendedCount}/4, Open={isOpenPalm}, Up={isOrientedUp}, Elev={isHandElevated} (PalmY={palmCenter.y:F2}), Conf={totalConfidence:P0}";

            return new HandsUpClassificationResult(
                isHandsUp: isSuccess,
                confidence: totalConfidence,
                rawGestureId: rawGesture,
                canonicalGestureId: canonicalGesture,
                extendedFingersCount: extendedCount,
                isOpenPalm: isOpenPalm,
                isHandElevated: isHandElevated,
                debugDetails: details);
        }

        /// <summary>
        /// Convenience factory for creating synthetic hands-up landmark sets for unit testing.
        /// </summary>
        public static HandLandmarkSet CreateSyntheticHandsUpLandmarks(float wristY = 0.50f, float fingerLength = 0.30f)
        {
            Vector3[] pts = new Vector3[21];
            Vector3 wrist = new Vector3(0.5f, wristY, 0f);
            pts[0] = wrist;

            // Base MCPs
            pts[1] = wrist + new Vector3(-0.06f, 0.05f, 0f); // Thumb CMC
            pts[2] = wrist + new Vector3(-0.09f, 0.10f, 0f); // Thumb MCP
            pts[3] = wrist + new Vector3(-0.11f, 0.15f, 0f); // Thumb IP
            pts[4] = wrist + new Vector3(-0.13f, 0.20f, 0f); // Thumb Tip

            pts[5] = wrist + new Vector3(-0.04f, 0.10f, 0f); // Index MCP
            pts[6] = wrist + new Vector3(-0.04f, 0.18f, 0f); // Index PIP
            pts[7] = wrist + new Vector3(-0.04f, 0.24f, 0f); // Index DIP
            pts[8] = wrist + new Vector3(-0.04f, 0.10f + fingerLength, 0f); // Index Tip

            pts[9] = wrist + new Vector3(0.00f, 0.11f, 0f); // Middle MCP
            pts[10] = wrist + new Vector3(0.00f, 0.19f, 0f); // Middle PIP
            pts[11] = wrist + new Vector3(0.00f, 0.26f, 0f); // Middle DIP
            pts[12] = wrist + new Vector3(0.00f, 0.11f + fingerLength, 0f); // Middle Tip

            pts[13] = wrist + new Vector3(0.04f, 0.10f, 0f); // Ring MCP
            pts[14] = wrist + new Vector3(0.04f, 0.18f, 0f); // Ring PIP
            pts[15] = wrist + new Vector3(0.04f, 0.24f, 0f); // Ring DIP
            pts[16] = wrist + new Vector3(0.04f, 0.10f + fingerLength * 0.95f, 0f); // Ring Tip

            pts[17] = wrist + new Vector3(0.08f, 0.08f, 0f); // Pinky MCP
            pts[18] = wrist + new Vector3(0.08f, 0.15f, 0f); // Pinky PIP
            pts[19] = wrist + new Vector3(0.08f, 0.20f, 0f); // Pinky DIP
            pts[20] = wrist + new Vector3(0.08f, 0.08f + fingerLength * 0.85f, 0f); // Pinky Tip

            return new HandLandmarkSet(pts, 1.0f);
        }

        /// <summary>
        /// Convenience factory for creating synthetic resting/fist landmarks (fingers curled).
        /// </summary>
        public static HandLandmarkSet CreateSyntheticCurledFistLandmarks(float wristY = 0.40f)
        {
            Vector3[] pts = new Vector3[21];
            Vector3 wrist = new Vector3(0.5f, wristY, 0f);
            pts[0] = wrist;

            // Curled fingers: tips curl back near MCPs, so dist(wrist, tip) is smaller than dist(wrist, pip)
            for (int f = 0; f < 4; f++)
            {
                int mcp = 5 + f * 4;
                int pip = 6 + f * 4;
                int dip = 7 + f * 4;
                int tip = 8 + f * 4;
                float xOff = (f - 1.5f) * 0.04f;

                pts[mcp] = wrist + new Vector3(xOff, 0.08f, 0f);
                pts[pip] = wrist + new Vector3(xOff, 0.15f, 0f);
                pts[dip] = wrist + new Vector3(xOff, 0.12f, 0f); // curled back
                pts[tip] = wrist + new Vector3(xOff, 0.09f, 0f); // curled down near MCP
            }

            pts[1] = wrist + new Vector3(-0.04f, 0.04f, 0f);
            pts[2] = wrist + new Vector3(-0.06f, 0.07f, 0f);
            pts[3] = wrist + new Vector3(-0.05f, 0.06f, 0f);
            pts[4] = wrist + new Vector3(-0.04f, 0.05f, 0f);

            return new HandLandmarkSet(pts, 1.0f);
        }
    }
}
