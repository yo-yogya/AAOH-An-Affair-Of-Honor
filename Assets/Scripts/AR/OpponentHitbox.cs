using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// Physical targeting hitbox for an AR opponent.
    /// Strictly decoupled from visual particle effects or character art.
    /// </summary>
    public class OpponentHitbox : MonoBehaviour
    {
        [Header("Hitbox Properties")]
        [Tooltip("Effective radius for touch/raycast targeting on mobile screen.")]
        public float hitboxRadius = 0.5f;
        public float hitboxHeight = 1.8f;
        public Vector3 centerOffset = new Vector3(0, 0.9f, 0);

        [Header("Targeting State")]
        public string targetId = "Target";
        public bool isLocalPOV = false;
        public bool isTargetLocked = false;
        [Tooltip("When enabled, renders a debug proxy and wireframe box. MUST be false in normal gameplay.")]
        public bool showDebugHitbox = false;

        public event Action<bool> OnTargetLockChanged;

        private MeshRenderer _proxyRenderer;

        private void Awake()
        {
            EnsureVisualMeshProxy();
        }

        public void EnsureVisualMeshProxy()
        {
            var existingProxy = transform.Find("Opponent_Visual_Proxy");
            if (existingProxy != null)
            {
                _proxyRenderer = existingProxy.GetComponent<MeshRenderer>();
                if (_proxyRenderer != null) _proxyRenderer.enabled = showDebugHitbox;
                UpdateProxyColor();
                return;
            }

            if (!showDebugHitbox) return; // Do not construct visible primitives during normal gameplay

            var proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            proxy.name = "Opponent_Visual_Proxy";
            proxy.transform.SetParent(transform, false);
            proxy.transform.localPosition = centerOffset;
            proxy.transform.localScale = new Vector3(hitboxRadius * 2f, hitboxHeight * 0.5f, hitboxRadius * 2f);

            var collider = proxy.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            _proxyRenderer = proxy.GetComponent<MeshRenderer>();
            _proxyRenderer.enabled = showDebugHitbox;
            UpdateProxyColor();
        }

        public void SetTargetLocked(bool locked)
        {
            if (isTargetLocked != locked)
            {
                isTargetLocked = locked;
                UpdateProxyColor();
                OnTargetLockChanged?.Invoke(locked);
            }
        }

        private void UpdateProxyColor()
        {
            if (_proxyRenderer == null || !_proxyRenderer.enabled) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = isTargetLocked ? new Color(0.2f, 0.9f, 0.3f, 0.85f) : new Color(0.85f, 0.35f, 0.35f, 0.85f);
                _proxyRenderer.material = mat;
            }
        }

        public Bounds GetWorldBounds()
        {
            return new Bounds(transform.position + centerOffset, new Vector3(hitboxRadius * 2f, hitboxHeight, hitboxRadius * 2f));
        }

        private void OnDrawGizmos()
        {
            if (!showDebugHitbox) return;
            Gizmos.color = isTargetLocked ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(transform.position + centerOffset, new Vector3(hitboxRadius * 2f, hitboxHeight, hitboxRadius * 2f));
        }
    }
}
