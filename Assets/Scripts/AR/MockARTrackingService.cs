using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Mock implementation of IARTrackingService and ISharedBattleSpaceProvider.
    /// Provides deterministic AR tracking simulation for developer iteration and testing.
    /// </summary>
    public class MockARTrackingService : IARTrackingService, ISharedBattleSpaceProvider
    {
        private bool _isARInitialized = true;
        private bool _isCalibrated = false;
        private ARTargetStatus _targetStatus;

        public bool IsARInitialized => _isARInitialized;
        public bool IsCalibrated => _isCalibrated;
        public ARTargetStatus TargetStatus => _targetStatus;

        public bool IsSpaceCalibrated => _isCalibrated;
        public Vector3 WorldOrigin { get; private set; } = Vector3.zero;
        public Quaternion WorldOrientation { get; private set; } = Quaternion.identity;

        public event Action<ARTargetStatus> OnTargetStatusUpdated;
        public event Action OnTargetAcquired;
        public event Action OnTargetLost;
        public event Action OnTargetReacquired;

        public MockARTrackingService()
        {
            _targetStatus = new ARTargetStatus
            {
                isVisible = false,
                confidence = ARTrackingConfidence.None,
                estimatedPosition = new Vector3(0, 0, 5.0f),
                estimatedOrientation = Quaternion.identity,
                distanceMeters = 5.0f,
                isHitboxAimed = false
            };
        }

        public void CalibrateSharedBattleSpace(Action<bool, string> onComplete)
        {
            _isCalibrated = true;
            WorldOrigin = Vector3.zero;
            WorldOrientation = Quaternion.identity;
            onComplete?.Invoke(true, "Mock shared battle-space calibrated successfully at (0,0,0).");
        }

        public void EstablishSpatialOrigin(Action<bool, string> onComplete)
        {
            CalibrateSharedBattleSpace(onComplete);
        }

        public void AlignWithPeer(string peerAnchorData, Action<bool, string> onComplete)
        {
            _isCalibrated = true;
            onComplete?.Invoke(true, "Aligned with peer mock anchor.");
        }

        public bool RaycastAimAtHitbox(Vector2 screenCenter)
        {
            // If target is visible, reticle aims on center
            if (_targetStatus.isVisible)
            {
                _targetStatus.isHitboxAimed = true;
                OnTargetStatusUpdated?.Invoke(_targetStatus);
                return true;
            }
            return false;
        }

        public void SimulateTargetAcquisition(Vector3 position, float distanceMeters)
        {
            bool wasVisible = _targetStatus.isVisible;
            _targetStatus.isVisible = true;
            _targetStatus.confidence = ARTrackingConfidence.High;
            _targetStatus.estimatedPosition = position;
            _targetStatus.distanceMeters = distanceMeters;
            _targetStatus.isHitboxAimed = true;

            OnTargetStatusUpdated?.Invoke(_targetStatus);

            if (!wasVisible)
            {
                OnTargetAcquired?.Invoke();
            }
        }

        public void SimulateTargetLost()
        {
            if (_targetStatus.isVisible)
            {
                _targetStatus.isVisible = false;
                _targetStatus.confidence = ARTrackingConfidence.None;
                _targetStatus.isHitboxAimed = false;

                OnTargetStatusUpdated?.Invoke(_targetStatus);
                OnTargetLost?.Invoke();
            }
        }

        public void SimulateTargetReacquired()
        {
            if (!_targetStatus.isVisible)
            {
                _targetStatus.isVisible = true;
                _targetStatus.confidence = ARTrackingConfidence.Medium;
                _targetStatus.isHitboxAimed = true;

                OnTargetStatusUpdated?.Invoke(_targetStatus);
                OnTargetReacquired?.Invoke();
            }
        }
    }
}
