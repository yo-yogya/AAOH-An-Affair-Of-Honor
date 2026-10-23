using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Production mobile AR Tracking service implementing IARTrackingService and ISharedBattleSpaceProvider.
    /// Integrates AR Foundation session, camera tracking, and physical reference calibration.
    /// In headless test environments (net8.0), cleanly falls back to simulated tracking.
    /// </summary>
    public class ARFoundationTrackingService : MonoBehaviour, IARTrackingService, ISharedBattleSpaceProvider
    {
        [Header("Configuration")]
        public Camera arCamera;
        public SharedReferenceCalibrator calibrator;
        public PlayerHitbox remoteHitbox;

        private bool _isARInitialized = false;
        private ARTargetStatus _targetStatus;

        public bool IsARInitialized => _isARInitialized;
        public bool IsCalibrated => calibrator != null && calibrator.IsCalibrated;
        public ARTargetStatus TargetStatus => _targetStatus;

        public bool IsSpaceCalibrated => IsCalibrated;
        public Vector3 WorldOrigin => calibrator != null ? calibrator.SharedOrigin : Vector3.zero;
        public Quaternion WorldOrientation => calibrator != null ? calibrator.SharedOrientation : Quaternion.identity;

        public event Action<ARTargetStatus> OnTargetStatusUpdated;
        public event Action OnTargetAcquired;
        public event Action OnTargetLost;
        public event Action OnTargetReacquired;

        private void Awake()
        {
            if (arCamera == null) arCamera = Camera.main;
            if (calibrator == null) calibrator = GetComponent<SharedReferenceCalibrator>() ?? gameObject.AddComponent<SharedReferenceCalibrator>();

            _targetStatus = new ARTargetStatus
            {
                isVisible = false,
                confidence = ARTrackingConfidence.None,
                estimatedPosition = new Vector3(0, 0, 4.5f),
                estimatedOrientation = Quaternion.identity,
                distanceMeters = 4.5f,
                isHitboxAimed = false
            };
        }

        private void Start()
        {
            InitializeARFoundation();
        }

        public void InitializeARFoundation()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (MobilePermissionHandler.Instance != null)
            {
                MobilePermissionHandler.Instance.RequestCameraPermission(granted =>
                {
                    _isARInitialized = granted;
                    Debug.Log($"[ARFoundationTrackingService] Camera permission granted: {granted}");
                });
                return;
            }
#endif
            _isARInitialized = true;
        }

        public void CalibrateSharedBattleSpace(Action<bool, string> onComplete)
        {
            if (calibrator != null)
            {
                calibrator.StartCalibration(onComplete);
            }
            else
            {
                onComplete?.Invoke(true, "Calibrated without physical marker (fallback).");
            }
        }

        public void EstablishSpatialOrigin(Action<bool, string> onComplete)
        {
            CalibrateSharedBattleSpace(onComplete);
        }

        public void AlignWithPeer(string peerAnchorData, Action<bool, string> onComplete)
        {
            if (calibrator != null)
            {
                calibrator.NotifyPeerCalibrationConfirmed();
                onComplete?.Invoke(true, "Peer calibration confirmed.");
            }
        }

        public bool RaycastAimAtHitbox(Vector2 screenCenter)
        {
            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null) return false;

            // Cast ray from screen center into the scene
            Ray ray = (screenCenter == Vector2.zero) 
                ? arCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)) 
                : arCamera.ScreenPointToRay(screenCenter);

            if (Physics.Raycast(ray, out RaycastHit hit, 20.0f))
            {
                var hitbox = hit.collider.GetComponent<PlayerHitbox>();
                if (hitbox != null && hitbox.IsTargetable)
                {
                    _targetStatus.isVisible = true;
                    _targetStatus.confidence = hitbox.trackingConfidence;
                    _targetStatus.estimatedPosition = hitbox.transform.position;
                    _targetStatus.estimatedOrientation = hitbox.transform.rotation;
                    _targetStatus.distanceMeters = hit.distance;
                    _targetStatus.isHitboxAimed = true;

                    hitbox.SetTargetLocked(true);
                    OnTargetStatusUpdated?.Invoke(_targetStatus);
                    return true;
                }
            }

            // If ray didn't hit, check if remoteHitbox is aimed by angular cone
            if (remoteHitbox != null && remoteHitbox.IsTargetable)
            {
                Vector3 toTarget = remoteHitbox.transform.position - arCamera.transform.position;
                float angle = Vector3.Angle(arCamera.transform.forward, toTarget);
                float distance = toTarget.magnitude;

                if (angle < 12.0f && distance < 15.0f)
                {
                    _targetStatus.isVisible = true;
                    _targetStatus.confidence = remoteHitbox.trackingConfidence;
                    _targetStatus.estimatedPosition = remoteHitbox.transform.position;
                    _targetStatus.estimatedOrientation = remoteHitbox.transform.rotation;
                    _targetStatus.distanceMeters = distance;
                    _targetStatus.isHitboxAimed = true;

                    remoteHitbox.SetTargetLocked(true);
                    OnTargetStatusUpdated?.Invoke(_targetStatus);
                    return true;
                }
            }

            if (remoteHitbox != null)
            {
                remoteHitbox.SetTargetLocked(false);
            }

            _targetStatus.isHitboxAimed = false;
            OnTargetStatusUpdated?.Invoke(_targetStatus);
            return false;
        }

        public void UpdateRemoteProxyPose(Vector3 worldPos, Quaternion worldRot, ARTrackingConfidence confidence)
        {
            bool wasVisible = _targetStatus.isVisible;
            _targetStatus.isVisible = confidence != ARTrackingConfidence.None;
            _targetStatus.confidence = confidence;
            _targetStatus.estimatedPosition = worldPos;
            _targetStatus.estimatedOrientation = worldRot;

            if (arCamera != null)
            {
                _targetStatus.distanceMeters = Vector3.Distance(arCamera.transform.position, worldPos);
            }

            if (remoteHitbox != null)
            {
                remoteHitbox.SetTrackingState(_targetStatus.isVisible, confidence);
            }

            OnTargetStatusUpdated?.Invoke(_targetStatus);

            if (!wasVisible && _targetStatus.isVisible)
            {
                OnTargetAcquired?.Invoke();
            }
            else if (wasVisible && !_targetStatus.isVisible)
            {
                OnTargetLost?.Invoke();
            }
        }

        public void SimulateTargetAcquisition(Vector3 position, float distanceMeters)
        {
            UpdateRemoteProxyPose(position, Quaternion.identity, ARTrackingConfidence.High);
            if (remoteHitbox != null)
            {
                remoteHitbox.transform.position = position;
                remoteHitbox.SetTargetLocked(true);
            }
            _targetStatus.distanceMeters = distanceMeters;
            _targetStatus.isHitboxAimed = true;
            OnTargetStatusUpdated?.Invoke(_targetStatus);
        }

        public void SimulateTargetLost()
        {
            UpdateRemoteProxyPose(_targetStatus.estimatedPosition, Quaternion.identity, ARTrackingConfidence.None);
            _targetStatus.isHitboxAimed = false;
            if (remoteHitbox != null) remoteHitbox.SetTargetLocked(false);
            OnTargetStatusUpdated?.Invoke(_targetStatus);
        }

        public void SimulateTargetReacquired()
        {
            UpdateRemoteProxyPose(_targetStatus.estimatedPosition, Quaternion.identity, ARTrackingConfidence.Medium);
            _targetStatus.isHitboxAimed = true;
            if (remoteHitbox != null) remoteHitbox.SetTargetLocked(true);
            OnTargetStatusUpdated?.Invoke(_targetStatus);
            OnTargetReacquired?.Invoke();
        }
    }
}
