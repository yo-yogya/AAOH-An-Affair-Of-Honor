using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    public enum CalibrationStep
    {
        Uncalibrated = 0,
        ScanningReference = 1,
        ReferenceDetected = 2,
        OriginLocked = 3,
        WaitingForOpponent = 4,
        SharedSpaceReady = 5
    }

    /// <summary>
    /// Contract for establishing a shared physical battle-space reference across two mobile devices.
    /// Shields the game from specific tracking hardware (image marker, spatial anchor, or manual fallback).
    /// </summary>
    public interface ISharedReferenceCalibrator
    {
        bool IsCalibrated { get; }
        CalibrationStep CurrentStep { get; }
        Vector3 SharedOrigin { get; }
        Quaternion SharedOrientation { get; }
        string CalibrationStatusText { get; }

        event Action<CalibrationStep, string> OnCalibrationStepChanged;
        event Action<bool, string> OnCalibrationCompleted;

        void StartCalibration(Action<bool, string> onComplete);
        void LockDetectedReference(Vector3 worldPos, Quaternion worldRot);
        void NotifyPeerCalibrationConfirmed();
        void SetManualOriginFallback(Vector3 origin, Quaternion orientation);
        void ResetCalibration();
    }
}
