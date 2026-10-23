using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Logical targeting hitbox for a player entity.
    /// Explicitly decoupled from visual character meshes and UI.
    /// In a two-phone session:
    /// - Phone A has PlayerHitbox_A (local, non-targetable) and PlayerHitbox_B (remote, targetable).
    /// - Phone B has PlayerHitbox_B (local, non-targetable) and PlayerHitbox_A (remote, targetable).
    /// The local player can NEVER target their own hitbox.
    /// </summary>
    public class PlayerHitbox : MonoBehaviour
    {
        [Header("Entity Identity")]
        public string ownerPlayerId = "Player_A";
        public bool isLocalPlayer = false;

        [Header("Hitbox Properties")]
        public float hitboxRadius = 0.5f;
        public float hitboxHeight = 1.8f;
        public Vector3 centerOffset = new Vector3(0, 0.9f, 0);
        [Tooltip("When enabled, renders a wireframe debug box. MUST be false in normal gameplay.")]
        public bool showDebugHitbox = false;

        [Header("Targeting State")]
        public bool isTargetLocked = false;
        public bool isTrackingValid = false;
        public ARTrackingConfidence trackingConfidence = ARTrackingConfidence.None;

        public string OwnerPlayerId
        {
            get => ownerPlayerId;
            set
            {
                ownerPlayerId = value;
                UpdateColliderConfiguration();
            }
        }

        public bool IsLocalPlayer
        {
            get => isLocalPlayer;
            set
            {
                isLocalPlayer = value;
                UpdateColliderConfiguration();
            }
        }

        public bool IsTargetable
        {
            get
            {
                if (isLocalPlayer || !isTrackingValid) return false;
                try
                {
                    return gameObject != null && gameObject.activeInHierarchy;
                }
                catch
                {
                    return true;
                }
            }
        }

        public event Action<bool> OnTargetLockChanged;
        public event Action<bool> OnTargetAvailabilityChanged;

        private CapsuleCollider _collider;

        private void Awake()
        {
            EnsureCollider();
            UpdateColliderConfiguration();
        }

        public void EnsureCollider()
        {
            try
            {
                if (_collider == null)
                {
                    _collider = GetComponent<CapsuleCollider>();
                    if (_collider == null && gameObject != null)
                    {
                        _collider = gameObject.AddComponent<CapsuleCollider>();
                    }
                }

                if (_collider != null)
                {
                    _collider.radius = hitboxRadius;
                    _collider.height = hitboxHeight;
                    _collider.center = centerOffset;
                    _collider.direction = 1; // Y-axis
                    _collider.isTrigger = true;
                }
            }
            catch { }
        }

        public void UpdateColliderConfiguration()
        {
            EnsureCollider();

            try
            {
                if (isLocalPlayer)
                {
                    // Local player hitbox is never targetable by local raycasts
                    if (gameObject != null) gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                    if (_collider != null) _collider.enabled = false;
                }
                else
                {
                    if (gameObject != null) gameObject.layer = LayerMask.NameToLayer("Default");
                    if (_collider != null) _collider.enabled = true;
                }
            }
            catch { }
        }

        public bool CanBeTargetedBy(string queryingPlayerId)
        {
            if (isLocalPlayer) return false;
            if (string.Equals(ownerPlayerId, queryingPlayerId, StringComparison.OrdinalIgnoreCase)) return false;
            return isTrackingValid;
        }

        public void SetTargetLocked(bool locked)
        {
            if (isTargetLocked != locked)
            {
                isTargetLocked = locked;
                OnTargetLockChanged?.Invoke(locked);
            }
        }

        public void SetTrackingState(bool valid, ARTrackingConfidence confidence)
        {
            bool wasTargetable = IsTargetable;
            isTrackingValid = valid;
            trackingConfidence = confidence;

            if (wasTargetable != IsTargetable)
            {
                OnTargetAvailabilityChanged?.Invoke(IsTargetable);
            }
        }

        public Bounds GetWorldBounds()
        {
            return new Bounds(transform.position + centerOffset, new Vector3(hitboxRadius * 2f, hitboxHeight, hitboxRadius * 2f));
        }

        private void OnDrawGizmos()
        {
            if (!showDebugHitbox || isLocalPlayer) return; // Hidden during normal gameplay

            Gizmos.color = isTargetLocked ? Color.green : (isTrackingValid ? Color.yellow : Color.red);
            Gizmos.DrawWireCube(transform.position + centerOffset, new Vector3(hitboxRadius * 2f, hitboxHeight, hitboxRadius * 2f));
        }
    }
}
