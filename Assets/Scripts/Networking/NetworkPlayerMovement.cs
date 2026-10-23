using System;
using UnityEngine;
using ArGuildEncounter.AR;
using ArGuildEncounter.Players;

namespace ArGuildEncounter.Networking
{
    /// <summary>
    /// NETWORK_PLAYER_MOVEMENT
    /// Synchronizes player position and rotation in shared-anchor space.
    /// Broadcasts local movements at a throttled tick rate (default 15 Hz).
    /// Smoothly interpolates remote player position without visual snapping.
    /// </summary>
    public class NetworkPlayerMovement : MonoBehaviour
    {
        [Header("Movement Configuration")]
        [SerializeField] private float _sendRateHz = 15.0f;
        [SerializeField] private float _interpolationSpeed = 8.0f;
        [SerializeField] private float _teleportThreshold = 2.0f;

        [Header("References")]
        [SerializeField] private PlayerCharacter _character;
        [SerializeField] private BattleArenaController _arenaController;
        [SerializeField] private LocalPvPPrototypeTransport _transport;

        private float _lastSendTime = 0f;
        private Vector3 _lastSentLocalPosition;
        private Vector3 _targetLocalPosition;
        private Quaternion _targetLocalRotation = Quaternion.identity;
        private bool _isRemote = false;

        public void Initialize(PlayerCharacter character, bool isRemote, LocalPvPPrototypeTransport transport, BattleArenaController arena)
        {
            _character = character;
            _isRemote = isRemote;
            _transport = transport;
            _arenaController = arena;

            if (_character != null)
            {
                _targetLocalPosition = _character.transform.localPosition;
                _targetLocalRotation = _character.transform.localRotation;
            }

            if (_isRemote && _transport != null)
            {
                _transport.OnRemoteSpatialReceived += HandleRemoteSpatialUpdate;
            }
        }

        private void OnDestroy()
        {
            if (_transport != null)
            {
                _transport.OnRemoteSpatialReceived -= HandleRemoteSpatialUpdate;
            }
        }

        private void Update()
        {
            if (_isRemote)
            {
                InterpolateRemoteMovement();
            }
            else
            {
                BroadcastLocalMovement();
            }
        }

        private void BroadcastLocalMovement()
        {
            if (_transport == null || _character == null) return;
            if (_transport.ConnectionState != NetworkConnectionState.Connected) return;

            float interval = 1.0f / _sendRateHz;
            if (Time.time - _lastSendTime < interval) return;

            Transform anchor = _arenaController != null ? _arenaController.SharedAnchorOrigin : transform.parent;
            Vector3 currentLocal = anchor != null ? anchor.InverseTransformPoint(_character.transform.position) : _character.transform.position;

            // Only broadcast if moved significantly (> 1cm)
            if (Vector3.SqrMagnitude(currentLocal - _lastSentLocalPosition) > 0.0001f)
            {
                _lastSentLocalPosition = currentLocal;
                _lastSendTime = Time.time;
                _transport.SendCombatActionIntent($"MOVE:{currentLocal.x:F3}:{currentLocal.y:F3}:{currentLocal.z:F3}");
            }
        }

        public void OnRemoteMovePayloadReceived(Vector3 newLocalPosition)
        {
            _targetLocalPosition = newLocalPosition;
        }

        private void HandleRemoteSpatialUpdate(Vector3 pos, Quaternion rot, ARTrackingConfidence conf, float timestamp)
        {
            _targetLocalPosition = pos;
            _targetLocalRotation = rot;
        }

        private void InterpolateRemoteMovement()
        {
            if (_character == null) return;

            Transform anchor = _arenaController != null ? _arenaController.SharedAnchorOrigin : transform.parent;
            Vector3 targetWorld = anchor != null ? anchor.TransformPoint(_targetLocalPosition) : _targetLocalPosition;

            float distance = Vector3.Distance(_character.transform.position, targetWorld);

            // Teleport if too far away to prevent prolonged sliding
            if (distance > _teleportThreshold)
            {
                _character.transform.position = targetWorld;
            }
            else
            {
                _character.transform.position = Vector3.Lerp(
                    _character.transform.position,
                    targetWorld,
                    Time.deltaTime * _interpolationSpeed);
            }
        }
    }
}
