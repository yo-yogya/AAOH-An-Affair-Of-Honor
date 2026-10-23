using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using ArGuildEncounter.Networking;
using ArGuildEncounter.Players;

namespace ArGuildEncounter.AR
{
    /// <summary>
    /// AR_PLAYER_PLACEMENT_CONTROLLER
    /// Handles AR raycast plane detection, placement indicator rendering,
    /// touch-driven character placement, and boundary clamping within BattleArenaController.
    /// Converts world coordinates to anchor-relative coordinates for multiplayer synchronization.
    /// </summary>
    public class ARPlayerPlacementController : MonoBehaviour
    {
        [Header("AR Foundation Dependencies")]
        [SerializeField] private ARRaycastManager _raycastManager;
        [SerializeField] private ARPlaneManager _planeManager;

        [Header("Arena & Anchor")]
        [SerializeField] private BattleArenaController _arenaController;
        [SerializeField] private Transform _sharedAnchor;

        [Header("Placement Visuals")]
        [SerializeField] private GameObject _placementIndicatorPrefab;
        [SerializeField] private float _indicatorScale = 0.5f;

        [Header("Controlled Player")]
        [SerializeField] private PlayerCharacter _localCharacter;

        [Header("Networking")]
        [SerializeField] private LocalPvPPrototypeTransport _networkTransport;

        private GameObject _indicatorInstance;
        private List<ARRaycastHit> _hits = new List<ARRaycastHit>();
        private bool _isCharacterPlaced = false;
        private Pose _latestPlacementPose;
        private bool _hasValidPlacementSurface = false;

        public bool IsCharacterPlaced => _isCharacterPlaced;
        public bool HasValidPlacementSurface => _hasValidPlacementSurface;
        public Pose LatestPlacementPose => _latestPlacementPose;

        public event Action<Vector3> OnLocalPlayerPlaced;
        public event Action<Vector3> OnLocalPlayerMoved;

        private void OnEnable()
        {
            TouchSimulation.Enable();
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            TouchSimulation.Disable();
            EnhancedTouchSupport.Disable();
        }

        private void Start()
        {
            CreatePlacementIndicator();
        }

        public void SetLocalCharacter(PlayerCharacter character)
        {
            _localCharacter = character;
        }

        public void SetSharedAnchor(Transform anchor)
        {
            _sharedAnchor = anchor;
        }

        public void ResetPlacement()
        {
            _isCharacterPlaced = false;
            if (_indicatorInstance != null)
            {
                _indicatorInstance.SetActive(true);
            }
        }

        private void Update()
        {
            UpdatePlacementIndicator();
            HandleTouchInput();
        }

        private void UpdatePlacementIndicator()
        {
            // Raycast from screen center to locate horizontal AR planes
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _hits.Clear();

            if (_raycastManager != null && _raycastManager.Raycast(screenCenter, _hits, TrackableType.PlaneWithinPolygon))
            {
                _latestPlacementPose = _hits[0].pose;
                _hasValidPlacementSurface = true;

                if (_indicatorInstance != null && !_isCharacterPlaced)
                {
                    _indicatorInstance.SetActive(true);
                    _indicatorInstance.transform.position = _latestPlacementPose.position;
                    _indicatorInstance.transform.rotation = _latestPlacementPose.rotation;
                }
            }
            else
            {
                _hasValidPlacementSurface = false;
                if (_indicatorInstance != null && !_isCharacterPlaced)
                {
                    _indicatorInstance.SetActive(false);
                }
            }
        }

        private void HandleTouchInput()
        {
            if (!TryGetTouchOrClick(out Vector2 screenPos, out int pointerId, out bool isMouse))
            {
                return;
            }

            // Ignore touches over UI elements
            if (EventSystem.current != null)
            {
                bool isPointerOverUI = isMouse
                    ? EventSystem.current.IsPointerOverGameObject(PointerInputModule.kMouseLeftId)
                    : EventSystem.current.IsPointerOverGameObject(pointerId);

                if (isPointerOverUI) return;
            }

            // Raycast on touched position
            _hits.Clear();
            if (_raycastManager != null && _raycastManager.Raycast(screenPos, _hits, TrackableType.PlaneWithinPolygon))
            {
                Pose touchHitPose = _hits[0].pose;
                ProcessPlacementOrMovement(touchHitPose.position);
            }
            else if (_hasValidPlacementSurface)
            {
                // Fallback to latest indicator pose if raycast missed polygon edges
                ProcessPlacementOrMovement(_latestPlacementPose.position);
            }
        }

        private void ProcessPlacementOrMovement(Vector3 worldPosition)
        {
            Transform anchor = _sharedAnchor != null ? _sharedAnchor : (_arenaController != null ? _arenaController.SharedAnchorOrigin : transform);
            Vector3 localPos = anchor.InverseTransformPoint(worldPosition);

            // Clamp inside arena bounds
            if (_arenaController != null)
            {
                localPos = _arenaController.ClampToArenaBounds(localPos);
            }

            Vector3 finalWorldPos = anchor.TransformPoint(localPos);

            if (_localCharacter != null)
            {
                _localCharacter.transform.position = finalWorldPos;
            }

            if (!_isCharacterPlaced)
            {
                _isCharacterPlaced = true;
                if (_indicatorInstance != null) _indicatorInstance.SetActive(false);
                OnLocalPlayerPlaced?.Invoke(localPos);

                // Transmit placement if network is connected
                if (_networkTransport != null && _networkTransport.ConnectionState == NetworkConnectionState.Connected)
                {
                    _networkTransport.SendCombatActionIntent($"PLACED:{localPos.x:F3}:{localPos.y:F3}:{localPos.z:F3}");
                }
            }
            else
            {
                OnLocalPlayerMoved?.Invoke(localPos);
            }
        }

        private bool TryGetTouchOrClick(out Vector2 screenPosition, out int pointerId, out bool isMouse)
        {
            screenPosition = Vector2.zero;
            pointerId = PointerInputModule.kMouseLeftId;
            isMouse = false;

            for (int i = 0; i < Touch.activeTouches.Count; i++)
            {
                var touch = Touch.activeTouches[i];
                if (touch.phase == TouchPhase.Began)
                {
                    screenPosition = touch.screenPosition;
                    pointerId = touch.touchId;
                    return true;
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                isMouse = true;
                return true;
            }

            return false;
        }

        private void CreatePlacementIndicator()
        {
            if (_placementIndicatorPrefab != null)
            {
                _indicatorInstance = Instantiate(_placementIndicatorPrefab);
            }
            else
            {
                // Procedural placeholder placement ring
                _indicatorInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _indicatorInstance.name = "PlacementIndicator_Placeholder";
                _indicatorInstance.transform.localScale = new Vector3(_indicatorScale, 0.01f, _indicatorScale);

                var col = _indicatorInstance.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var renderer = _indicatorInstance.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                    mat.color = new Color(0.2f, 0.9f, 0.3f, 0.5f);
                    renderer.material = mat;
                }
            }

            _indicatorInstance.SetActive(false);
        }
    }
}
