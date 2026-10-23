using System;
using UnityEngine;

namespace ArGuildEncounter.Gesture
{
    public enum GestureValidationStatus
    {
        InProgress = 0,
        Success = 1,
        Fail = 2,
        Timeout = 3
    }

    /// <summary>
    /// GESTURE_VALIDATOR_INTERFACE
    /// Decouples gesture validation from combat damage calculations.
    /// Returns standardized Success, Fail, or Timeout statuses.
    /// </summary>
    public interface IGestureValidator
    {
        GestureValidationStatus CurrentStatus { get; }
        string ActiveGestureId { get; }
        float TimeRemaining { get; }

        event Action<GestureValidationStatus, float> OnValidationFinished;

        void BeginValidation(string expectedGestureId, float timeLimitSeconds);
        void SubmitGestureAttempt(string performedGestureId, float confidence);
        void CancelValidation();
    }

    /// <summary>
    /// PLACEHOLDER_GESTURE_VALIDATOR
    /// Functional runtime gesture validator.
    /// Evaluates somatic gestures against confidence thresholds or times out when window expires.
    /// </summary>
    public class PlaceholderGestureValidator : MonoBehaviour, IGestureValidator
    {
        [SerializeField] private float _minimumConfidence = 0.70f;

        private GestureValidationStatus _status = GestureValidationStatus.InProgress;
        private string _activeGestureId;
        private float _timeRemaining;
        private bool _isActive = false;

        public GestureValidationStatus CurrentStatus => _status;
        public string ActiveGestureId => _activeGestureId;
        public float TimeRemaining => _timeRemaining;

        public event Action<GestureValidationStatus, float> OnValidationFinished;

        public void BeginValidation(string expectedGestureId, float timeLimitSeconds)
        {
            _activeGestureId = expectedGestureId;
            _timeRemaining = timeLimitSeconds;
            _status = GestureValidationStatus.InProgress;
            _isActive = true;
        }

        public void SubmitGestureAttempt(string performedGestureId, float confidence)
        {
            if (!_isActive) return;

            bool isMatch = string.Equals(_activeGestureId, performedGestureId, StringComparison.OrdinalIgnoreCase);

            if (isMatch && confidence >= _minimumConfidence)
            {
                CompleteValidation(GestureValidationStatus.Success, confidence);
            }
            else
            {
                CompleteValidation(GestureValidationStatus.Fail, confidence);
            }
        }

        public void CancelValidation()
        {
            _isActive = false;
            _status = GestureValidationStatus.InProgress;
        }

        private void Update()
        {
            if (!_isActive) return;

            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                CompleteValidation(GestureValidationStatus.Timeout, 0f);
            }
        }

        private void CompleteValidation(GestureValidationStatus status, float confidence)
        {
            _isActive = false;
            _status = status;
            OnValidationFinished?.Invoke(_status, confidence);
        }
    }
}
