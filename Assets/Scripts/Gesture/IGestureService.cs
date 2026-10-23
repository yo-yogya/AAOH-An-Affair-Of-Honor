using System;

namespace ArGuildEncounter.Gesture
{
    /// <summary>
    /// Structured result produced by the gesture recognition pipeline.
    /// Strictly decoupled from spell damage and combat resolution.
    /// </summary>
    public struct GestureRecognitionResult
    {
        public string gestureId;
        public float confidence;
        public float timestamp;
        public bool isSuccess;

        public GestureRecognitionResult(string gestureId, float confidence, float timestamp, bool isSuccess)
        {
            this.gestureId = gestureId;
            this.confidence = confidence;
            this.timestamp = timestamp;
            this.isSuccess = isSuccess;
        }
    }

    /// <summary>
    /// Abstract interface for gesture recognition.
    /// Shields the game engine from underlying camera CV or machine learning models.
    /// </summary>
    public interface IGestureService
    {
        bool IsTrackingGesture { get; }
        string ExpectedGestureId { get; }

        event Action<GestureRecognitionResult> OnGestureRecognized;
        event Action OnGestureFailed;
        event Action OnGestureWindowTimedOut;

        void StartGestureRecognition(string expectedGestureId, float timeWindowSeconds);
        void StopGestureRecognition();
        void SimulateGesturePerformed(string performedGestureId, float confidence);
        void SimulateGestureFailure();
        void SimulateTimeout();
    }
}
