using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Calibration controller using a shared physical reference (visual marker / anchor).
    /// Enforces that both devices align to the same physical reference rather than
    /// arbitrarily picking independent floor points.
    /// Includes a clearly labeled DEV MODE manual fallback.
    /// </summary>
    public class SharedReferenceCalibrator : MonoBehaviour, ISharedReferenceCalibrator
    {
        [Header("State")]
        [SerializeField] private CalibrationStep _currentStep = CalibrationStep.Uncalibrated;
        [SerializeField] private string _statusText = "Not Calibrated";

        public bool IsCalibrated => _currentStep == CalibrationStep.SharedSpaceReady;
        public CalibrationStep CurrentStep => _currentStep;
        public Vector3 SharedOrigin => _battleSpace.OriginPosition;
        public Quaternion SharedOrientation => _battleSpace.OriginRotation;
        public string CalibrationStatusText => _statusText;

        public event Action<CalibrationStep, string> OnCalibrationStepChanged;
        public event Action<bool, string> OnCalibrationCompleted;

        private readonly SharedBattleSpace _battleSpace = new SharedBattleSpace();
        public SharedBattleSpace BattleSpace => _battleSpace;

        private bool _isLocalConfirmed = false;
        private bool _isPeerConfirmed = false;
        private Action<bool, string> _pendingCallback;

        public void StartCalibration(Action<bool, string> onComplete)
        {
            _pendingCallback = onComplete;
            _isLocalConfirmed = false;
            _isPeerConfirmed = false;
            _battleSpace.Reset();

            SetStep(CalibrationStep.ScanningReference, "POINT AR CAMERA AT SHARED PHYSICAL REFERENCE");
        }

        public void LockDetectedReference(Vector3 worldPos, Quaternion worldRot)
        {
            if (_currentStep == CalibrationStep.SharedSpaceReady) return;

            _battleSpace.SetOrigin(worldPos, worldRot);
            _isLocalConfirmed = true;

            SetStep(CalibrationStep.OriginLocked, $"REFERENCE LOCKED AT {worldPos}. WAITING FOR PEER...");

            if (_isPeerConfirmed)
            {
                CompleteSharedSpaceReady();
            }
            else
            {
                SetStep(CalibrationStep.WaitingForOpponent, "WAITING FOR OPPONENT CALIBRATION...");
            }
        }

        public void NotifyPeerCalibrationConfirmed()
        {
            _isPeerConfirmed = true;

            if (_isLocalConfirmed && _currentStep != CalibrationStep.SharedSpaceReady)
            {
                CompleteSharedSpaceReady();
            }
        }

        public void SetManualOriginFallback(Vector3 origin, Quaternion orientation)
        {
            Debug.LogWarning("[DEV MODE] Using Manual Shared Origin Fallback.");
            _battleSpace.SetOrigin(origin, orientation);
            _isLocalConfirmed = true;
            _isPeerConfirmed = true;
            CompleteSharedSpaceReady("DEV MODE: Manual Shared Origin Established");
        }

        private void CompleteSharedSpaceReady(string message = "SHARED BATTLE SPACE READY")
        {
            SetStep(CalibrationStep.SharedSpaceReady, message);
            _pendingCallback?.Invoke(true, message);
            OnCalibrationCompleted?.Invoke(true, message);
            _pendingCallback = null;
        }

        public void ResetCalibration()
        {
            _isLocalConfirmed = false;
            _isPeerConfirmed = false;
            _battleSpace.Reset();
            _pendingCallback = null;
            SetStep(CalibrationStep.Uncalibrated, "Awaiting calibration");
        }

        private void SetStep(CalibrationStep step, string message)
        {
            _currentStep = step;
            _statusText = message;
            OnCalibrationStepChanged?.Invoke(step, message);
        }
    }
}
