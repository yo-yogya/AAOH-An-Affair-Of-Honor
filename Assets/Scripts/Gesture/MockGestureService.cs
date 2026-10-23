using System;
using UnityEngine;

namespace ArGuildEncounter.Gesture
{
    /// <summary>
    /// Mock implementation of IGestureService.
    /// Provides deterministic gesture validation and simulation for testing before ML model integration.
    /// </summary>
    public class MockGestureService : IGestureService
    {
        private bool _isTracking = false;
        private string _expectedGestureId = "";
        private float _windowSeconds = 0f;
        private readonly Func<float> _timeProvider;

        public bool IsTrackingGesture => _isTracking;
        public string ExpectedGestureId => _expectedGestureId;

        public event Action<GestureRecognitionResult> OnGestureRecognized;
        public event Action OnGestureFailed;
        public event Action OnGestureWindowTimedOut;

        public MockGestureService(Func<float> timeProvider = null)
        {
            _timeProvider = timeProvider ?? (() => (float)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
        }

        public void StartGestureRecognition(string expectedGestureId, float timeWindowSeconds)
        {
            _isTracking = true;
            _expectedGestureId = expectedGestureId;
            _windowSeconds = timeWindowSeconds;
        }

        public void StopGestureRecognition()
        {
            _isTracking = false;
        }

        public void SimulateGesturePerformed(string performedGestureId, float confidence)
        {
            if (!_isTracking) return;

            bool isMatch = string.Equals(_expectedGestureId, performedGestureId, StringComparison.OrdinalIgnoreCase);
            var result = new GestureRecognitionResult(
                performedGestureId, 
                confidence, 
                _timeProvider(), 
                isMatch && confidence >= 0.70f);

            _isTracking = false;

            if (result.isSuccess)
            {
                OnGestureRecognized?.Invoke(result);
            }
            else
            {
                OnGestureFailed?.Invoke();
            }
        }

        public void SimulateGestureFailure()
        {
            if (!_isTracking) return;
            _isTracking = false;
            OnGestureFailed?.Invoke();
        }

        public void SimulateGestureFailed() => SimulateGestureFailure();

        public void SimulateTimeout()
        {
            if (!_isTracking) return;
            _isTracking = false;
            OnGestureWindowTimedOut?.Invoke();
        }
    }
}
