using System;
using UnityEngine;
using ArGuildEncounter.AR;

namespace ArGuildEncounter.Combat
{
    public enum TargetAimState
    {
        Invalid = 0,
        OutOfRange = 1,
        Valid = 2,
        Locked = 3
    }

    /// <summary>
    /// TARGETING_CONTROLLER
    /// Controls AR reticle targeting and raycast hitbox verification.
    /// Provides 4 clear targeting states: Invalid, OutOfRange, Valid, Locked.
    /// </summary>
    public class TargetingController : MonoBehaviour
    {
        [Header("Targeting Parameters")]
        [SerializeField] private float _maxTargetRangeMeters = 8.0f;
        [SerializeField] private float _lockDurationRequiredSeconds = 0.4f;
        [SerializeField] private Camera _arCamera;

        [Header("Opponent Hitbox")]
        [SerializeField] private OpponentHitbox _targetHitbox;

        private TargetAimState _aimState = TargetAimState.Invalid;
        private float _currentLockTime = 0f;
        private float _distanceToTarget = 0f;

        public TargetAimState AimState => _aimState;
        public float DistanceToTarget => _distanceToTarget;
        public bool IsLocked => _aimState == TargetAimState.Locked;

        public event Action<TargetAimState> OnAimStateChanged;

        private void Awake()
        {
            if (_arCamera == null)
            {
                _arCamera = Camera.main;
            }
        }

        public void SetTargetHitbox(OpponentHitbox hitbox)
        {
            _targetHitbox = hitbox;
        }

        public void UpdateAim(Vector2 screenPosition)
        {
            if (_targetHitbox == null || _arCamera == null)
            {
                SetAimState(TargetAimState.Invalid);
                return;
            }

            Ray ray = _arCamera.ScreenPointToRay(screenPosition);
            _distanceToTarget = Vector3.Distance(_arCamera.transform.position, _targetHitbox.transform.position);

            if (_distanceToTarget > _maxTargetRangeMeters)
            {
                _currentLockTime = 0f;
                SetAimState(TargetAimState.OutOfRange);
                return;
            }

            // Raycast against hitbox collider or bounds
            bool isRayHittingHitbox = false;
            if (Physics.Raycast(ray, out RaycastHit hit, _maxTargetRangeMeters))
            {
                if (hit.collider.gameObject == _targetHitbox.gameObject || hit.collider.transform.IsChildOf(_targetHitbox.transform))
                {
                    isRayHittingHitbox = true;
                }
            }
            else
            {
                // Geometric cone tolerance test for mobile AR ease-of-use
                Vector3 toTarget = (_targetHitbox.transform.position - _arCamera.transform.position).normalized;
                float angle = Vector3.Angle(ray.direction, toTarget);
                if (angle < 12.0f) // 12-degree targeting cone
                {
                    isRayHittingHitbox = true;
                }
            }

            if (isRayHittingHitbox)
            {
                _currentLockTime += Time.deltaTime;
                if (_currentLockTime >= _lockDurationRequiredSeconds)
                {
                    SetAimState(TargetAimState.Locked);
                }
                else
                {
                    SetAimState(TargetAimState.Valid);
                }
            }
            else
            {
                _currentLockTime = 0f;
                SetAimState(TargetAimState.Invalid);
            }
        }

        public void EvaluateAimDirect(float distance, bool isRayHittingHitbox, float deltaTime = 0.5f)
        {
            _distanceToTarget = distance;
            if (_distanceToTarget > _maxTargetRangeMeters)
            {
                _currentLockTime = 0f;
                SetAimState(TargetAimState.OutOfRange);
                return;
            }

            if (isRayHittingHitbox)
            {
                _currentLockTime += deltaTime;
                if (_currentLockTime >= _lockDurationRequiredSeconds)
                {
                    SetAimState(TargetAimState.Locked);
                }
                else
                {
                    SetAimState(TargetAimState.Valid);
                }
            }
            else
            {
                _currentLockTime = 0f;
                SetAimState(TargetAimState.Invalid);
            }
        }

        private void SetAimState(TargetAimState newState)
        {
            if (_aimState != newState)
            {
                _aimState = newState;
                OnAimStateChanged?.Invoke(_aimState);
            }
        }
    }
}
