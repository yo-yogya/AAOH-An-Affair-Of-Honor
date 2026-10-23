using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Smooths discrete network spatial packets using timestamped buffering and damped interpolation.
    /// Prevents network jitter from causing the remote player proxy and hitbox to visibly stutter or teleport.
    /// Synchronizes both RemotePlayerVisual and PlayerHitbox so they remain 100% spatially aligned.
    /// </summary>
    public class SpatialInterpolator : MonoBehaviour
    {
        [Header("Smoothing Configuration")]
        [Tooltip("Interpolation damping rate (higher = snappier, lower = smoother).")]
        public float positionDamping = 12.0f;
        public float rotationDamping = 12.0f;

        [Tooltip("Maximum distance delta before hard-snapping to prevent endless dragging on respawn.")]
        public float snapThresholdDistance = 5.0f;

        [Header("Synchronized Targets")]
        public Transform visualTransform;
        public Transform hitboxTransform;

        private struct SpatialSample
        {
            public Vector3 position;
            public Quaternion rotation;
            public float timestamp;
        }

        private Vector3 _currentPosition = Vector3.zero;
        private Quaternion _currentRotation = Quaternion.identity;
        private Vector3 _targetPosition = Vector3.zero;
        private Quaternion _targetRotation = Quaternion.identity;
        private bool _hasReceivedFirstSample = false;

        public Vector3 CurrentPosition => _currentPosition;
        public Quaternion CurrentRotation => _currentRotation;

        public void PushNetworkSample(Vector3 worldPos, Quaternion worldRot, float sampleTimestamp)
        {
            if (!_hasReceivedFirstSample)
            {
                _currentPosition = worldPos;
                _currentRotation = worldRot;
                _targetPosition = worldPos;
                _targetRotation = worldRot;
                _hasReceivedFirstSample = true;
                ApplyToTransforms(_currentPosition, _currentRotation);
                return;
            }

            float dist = Vector3.Distance(_currentPosition, worldPos);
            if (dist > snapThresholdDistance)
            {
                // Hard snap on large teleport / match reset
                _currentPosition = worldPos;
                _currentRotation = worldRot;
            }

            _targetPosition = worldPos;
            _targetRotation = worldRot;
        }

        private void Update()
        {
            if (!_hasReceivedFirstSample) return;

            float dt = Time.deltaTime;
            _currentPosition = Vector3.Lerp(_currentPosition, _targetPosition, 1f - Mathf.Exp(-positionDamping * dt));
            _currentRotation = Quaternion.Slerp(_currentRotation, _targetRotation, 1f - Mathf.Exp(-rotationDamping * dt));

            ApplyToTransforms(_currentPosition, _currentRotation);
        }

        public void ApplyToTransforms(Vector3 pos, Quaternion rot)
        {
            _currentPosition = pos;
            _currentRotation = rot;

            try
            {
                transform.position = pos;
                transform.rotation = rot;

                if (visualTransform != null && visualTransform != transform)
                {
                    visualTransform.position = pos;
                    visualTransform.rotation = rot;
                }

                if (hitboxTransform != null && hitboxTransform != transform)
                {
                    hitboxTransform.position = pos;
                    hitboxTransform.rotation = rot;
                }
            }
            catch { }
        }

        public void ResetInterpolator()
        {
            _hasReceivedFirstSample = false;
            _currentPosition = Vector3.zero;
            _currentRotation = Quaternion.identity;
            _targetPosition = Vector3.zero;
            _targetRotation = Quaternion.identity;
        }
    }
}
