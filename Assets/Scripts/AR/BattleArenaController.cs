using System;
using UnityEngine;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// BATTLE_ARENA_CONTROLLER
    /// Defines and manages the physical AR battlefield boundaries, spawn positions,
    /// and spatial reference frame relative to the localized shared anchor.
    /// Enforces movement bounds so players remain within the combat zone.
    /// </summary>
    public class BattleArenaController : MonoBehaviour
    {
        [Header("Arena Dimensions (Meters)")]
        [SerializeField] private Vector2 _arenaDimensions = new Vector2(4.0f, 6.0f);
        [SerializeField] private float _boundaryHeight = 2.0f;

        [Header("Spawn Offsets (Anchor Relative)")]
        [SerializeField] private Vector3 _hostSpawnOffset = new Vector3(0f, 0f, -1.8f);
        [SerializeField] private Vector3 _clientSpawnOffset = new Vector3(0f, 0f, 1.8f);

        [Header("Boundary Visualization")]
        [SerializeField] private LineRenderer _boundaryLineRenderer;
        [SerializeField] private Color _boundaryColor = new Color(0.2f, 0.8f, 1.0f, 0.6f);
        [SerializeField] private bool _showTargetRings = true;

        [Header("Shared Anchor Reference")]
        [SerializeField] private Transform _sharedAnchorOrigin;

        public Vector2 ArenaDimensions => _arenaDimensions;
        public float BoundaryHeight => _boundaryHeight;
        public bool ShowTargetRings => _showTargetRings;
        public Vector3 HostSpawnPosition => _sharedAnchorOrigin != null ? _sharedAnchorOrigin.TransformPoint(_hostSpawnOffset) : transform.TransformPoint(_hostSpawnOffset);
        public Vector3 ClientSpawnPosition => _sharedAnchorOrigin != null ? _sharedAnchorOrigin.TransformPoint(_clientSpawnOffset) : transform.TransformPoint(_clientSpawnOffset);
        public Vector3 HostSpawnOffsetLocal => _hostSpawnOffset;
        public Vector3 ClientSpawnOffsetLocal => _clientSpawnOffset;
        public Transform SharedAnchorOrigin => _sharedAnchorOrigin;

        public event Action<bool> OnArenaCalibrated;

        private void Awake()
        {
            if (_sharedAnchorOrigin == null)
            {
                _sharedAnchorOrigin = transform;
            }
            SetupBoundaryVisualization();
        }

        public void SetSharedAnchor(Transform anchor)
        {
            if (anchor == null) return;
            _sharedAnchorOrigin = anchor;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            OnArenaCalibrated?.Invoke(true);
        }

        public Vector3 ClampToArenaBounds(Vector3 localPosition)
        {
            float halfWidth = _arenaDimensions.x * 0.5f;
            float halfLength = _arenaDimensions.y * 0.5f;

            float clampedX = Mathf.Clamp(localPosition.x, -halfWidth, halfWidth);
            float clampedZ = Mathf.Clamp(localPosition.z, -halfLength, halfLength);

            return new Vector3(clampedX, localPosition.y, clampedZ);
        }

        public bool IsInsideArena(Vector3 localPosition)
        {
            float halfWidth = _arenaDimensions.x * 0.5f;
            float halfLength = _arenaDimensions.y * 0.5f;

            return localPosition.x >= -halfWidth && localPosition.x <= halfWidth &&
                   localPosition.z >= -halfLength && localPosition.z <= halfLength;
        }

        private void SetupBoundaryVisualization()
        {
            if (_boundaryLineRenderer == null)
            {
                _boundaryLineRenderer = GetComponent<LineRenderer>();
                if (_boundaryLineRenderer == null)
                {
                    _boundaryLineRenderer = gameObject.AddComponent<LineRenderer>();
                }
            }

            _boundaryLineRenderer.positionCount = 5;
            _boundaryLineRenderer.loop = true;
            _boundaryLineRenderer.useWorldSpace = false;
            _boundaryLineRenderer.startWidth = 0.03f;
            _boundaryLineRenderer.endWidth = 0.03f;

            Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            lineMat.color = _boundaryColor;
            _boundaryLineRenderer.material = lineMat;

            float hw = _arenaDimensions.x * 0.5f;
            float hl = _arenaDimensions.y * 0.5f;

            _boundaryLineRenderer.SetPosition(0, new Vector3(-hw, 0.01f, -hl));
            _boundaryLineRenderer.SetPosition(1, new Vector3(hw, 0.01f, -hl));
            _boundaryLineRenderer.SetPosition(2, new Vector3(hw, 0.01f, hl));
            _boundaryLineRenderer.SetPosition(3, new Vector3(-hw, 0.01f, hl));
            _boundaryLineRenderer.SetPosition(4, new Vector3(-hw, 0.01f, -hl));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 center = transform.position;
            Gizmos.DrawWireCube(center, new Vector3(_arenaDimensions.x, 0.05f, _arenaDimensions.y));

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(HostSpawnPosition, 0.3f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(ClientSpawnPosition, 0.3f);
        }
    }
}
