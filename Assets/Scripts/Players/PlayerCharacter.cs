using System;
using UnityEngine;
using ArGuildEncounter.AR;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Guilds;

namespace ArGuildEncounter.Players
{
    /// <summary>
    /// PLAYER_CHARACTER
    /// Decoupled, replaceable visual character abstraction.
    /// Separates presentation (CharacterView, Effects, UIAnchor) from gameplay logic
    /// (PlayerCombatState, Hitbox, Network synchronization).
    /// Allows 3D models to be cleanly swapped without touching combat or networking.
    /// </summary>
    public class PlayerCharacter : MonoBehaviour
    {
        [Header("Hierarchy Roots")]
        [SerializeField] private Transform _gameplayRoot;
        [SerializeField] private Transform _characterViewRoot;
        [SerializeField] private Transform _effectsRoot;
        [SerializeField] private Transform _targetRingRoot;
        [SerializeField] private Transform _uiAnchorRoot;

        [Header("Components")]
        [SerializeField] private OpponentHitbox _hitbox;
        [SerializeField] private PlayerHitbox _playerHitbox;
        [SerializeField] private Renderer _modelRenderer;

        [Header("Model Calibration & AR Scaling")]
        public float targetHeight = 1.80f;
        public float visualScale = 1.8361f;
        public float groundOffset = 0.90f;

        [Header("Identity")]
        [SerializeField] private string _playerId = "Player";
        [SerializeField] private bool _isLocalPlayer = true;
        [SerializeField] private bool _isHost = true;

        [Header("Target Indicator Visuals")]
        [SerializeField] private GameObject _targetRingVisual;
        [SerializeField] private Material _defaultMaterial;
        [SerializeField] private Material _targetedMaterial;

        public string PlayerId => _playerId;
        public bool IsLocalPlayer => _isLocalPlayer;
        public bool IsHost => _isHost;
        public Transform GameplayRoot => _gameplayRoot != null ? _gameplayRoot : transform;
        public Transform CharacterViewRoot => _characterViewRoot != null ? _characterViewRoot : transform;
        public Transform UIAnchorRoot => _uiAnchorRoot != null ? _uiAnchorRoot : transform;
        public OpponentHitbox Hitbox => _hitbox;
        public PlayerHitbox PlayerHitbox => _playerHitbox != null ? _playerHitbox : GetComponentInChildren<PlayerHitbox>();

        public PlayerCombatState CombatState { get; private set; }

        public event Action<Vector3> OnPositionChanged;
        public event Action<bool> OnTargetStateChanged;

        private void Awake()
        {
            EnsureHierarchy();
        }

        public void Initialize(string playerId, bool isLocal, bool isHost, GuildData guild, PlayerCombatState combatState = null)
        {
            _playerId = playerId;
            _isLocalPlayer = isLocal;
            _isHost = isHost;
            CombatState = combatState;

            gameObject.name = $"PlayerCharacter_{(isLocal ? "Local" : "Remote")}";

            EnsureHierarchy();

            if (_hitbox != null)
            {
                _hitbox.targetId = playerId;
                _hitbox.isLocalPOV = isLocal;
                _hitbox.showDebugHitbox = false;
            }

            if (_playerHitbox != null)
            {
                _playerHitbox.OwnerPlayerId = playerId;
                _playerHitbox.IsLocalPlayer = isLocal;
                _playerHitbox.showDebugHitbox = false;
            }

            ApplyGuildVisuals(guild);
            HookHitboxEvents();
            SetTargetRingActive(false);
        }

        private void HookHitboxEvents()
        {
            if (_playerHitbox != null)
            {
                _playerHitbox.OnTargetLockChanged -= SetTargetRingActive;
                _playerHitbox.OnTargetLockChanged += SetTargetRingActive;
            }
            if (_hitbox != null)
            {
                _hitbox.OnTargetLockChanged -= SetTargetRingActive;
                _hitbox.OnTargetLockChanged += SetTargetRingActive;
            }
        }

        private void OnDestroy()
        {
            if (_playerHitbox != null) _playerHitbox.OnTargetLockChanged -= SetTargetRingActive;
            if (_hitbox != null) _hitbox.OnTargetLockChanged -= SetTargetRingActive;
        }

        private void EnsureTargetRingMaterial()
        {
            if (_targetRingVisual == null) return;
            var mr = _targetRingVisual.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(0.2f, 1.0f, 0.5f, 0.8f);
                    mr.material = mat;
                }
            }
        }

        public void SetTargetRingActive(bool active)
        {
            if (_targetRingVisual != null)
            {
                _targetRingVisual.SetActive(active);
                if (active) EnsureTargetRingMaterial();
            }
            OnTargetStateChanged?.Invoke(active);
        }

        public void SetPositionAnchorRelative(Vector3 localPos, Transform sharedAnchor)
        {
            if (sharedAnchor != null)
            {
                transform.position = sharedAnchor.TransformPoint(localPos);
            }
            else
            {
                transform.localPosition = localPos;
            }
            OnPositionChanged?.Invoke(transform.position);
        }

        public Vector3 GetPositionAnchorRelative(Transform sharedAnchor)
        {
            if (sharedAnchor != null)
            {
                return sharedAnchor.InverseTransformPoint(transform.position);
            }
            return transform.localPosition;
        }

        public void PlayHitFeedback()
        {
            // Flash red on hit
            if (_modelRenderer != null)
            {
                StartCoroutine(HitFlashCoroutine());
            }
        }

        private System.Collections.IEnumerator HitFlashCoroutine()
        {
            if (_modelRenderer == null) yield break;
            Color original = _modelRenderer.material.HasProperty("_BaseColor") 
                ? _modelRenderer.material.GetColor("_BaseColor") 
                : _modelRenderer.material.color;

            if (_modelRenderer.material.HasProperty("_BaseColor"))
                _modelRenderer.material.SetColor("_BaseColor", Color.red);
            else
                _modelRenderer.material.color = Color.red;

            yield return new WaitForSeconds(0.15f);

            if (_modelRenderer != null)
            {
                if (_modelRenderer.material.HasProperty("_BaseColor"))
                    _modelRenderer.material.SetColor("_BaseColor", original);
                else
                    _modelRenderer.material.color = original;
            }
        }

        private void ApplyGuildVisuals(GuildData guild)
        {
            if (_modelRenderer == null && _characterViewRoot != null)
            {
                _modelRenderer = _characterViewRoot.GetComponentInChildren<Renderer>();
            }

            if (_modelRenderer == null || guild == null) return;

            Color guildColor = Color.white;
            switch (guild.guildType)
            {
                case GuildType.Magma:
                    guildColor = new Color(1f, 0.35f, 0.1f); // Ember Orange
                    break;
                case GuildType.Aqua:
                    guildColor = new Color(0.15f, 0.6f, 1f); // Deep Cyan/Blue
                    break;
                case GuildType.Plasma:
                    guildColor = new Color(0.85f, 0.2f, 1f); // Violet Lightning
                    break;
                case GuildType.HolyTrinity:
                    guildColor = new Color(1f, 0.95f, 0.6f); // Radiant Gold
                    break;
                case GuildType.Gambit:
                    guildColor = new Color(0.2f, 0.9f, 0.4f); // Emerald Jade
                    break;
            }

            Color guildTint = Color.Lerp(Color.white, guildColor, 0.35f);

            if (!Application.isPlaying)
            {
                var block = new MaterialPropertyBlock();
                _modelRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", guildTint);
                block.SetColor("_Color", guildTint);
                _modelRenderer.SetPropertyBlock(block);
                return;
            }

            if (_modelRenderer.material != null)
            {
                if (_modelRenderer.material.HasProperty("_BaseColor"))
                {
                    _modelRenderer.material.SetColor("_BaseColor", guildTint);
                }
                if (_modelRenderer.material.HasProperty("_Color"))
                {
                    _modelRenderer.material.color = guildTint;
                }
            }
        }

        private void Start()
        {
            EnsureHierarchy();
            if (CombatState?.Guild != null)
            {
                ApplyGuildVisuals(CombatState.Guild);
            }
        }

        private void EnsureHierarchy()
        {
            if (_gameplayRoot == null)
            {
                var gp = transform.Find("GameplayRoot");
                if (gp == null)
                {
                    var go = new GameObject("GameplayRoot");
                    go.transform.SetParent(transform, false);
                    _gameplayRoot = go.transform;
                }
                else _gameplayRoot = gp;
            }

            if (_characterViewRoot == null)
            {
                var view = transform.Find("CharacterView");
                if (view == null)
                {
                    var go = new GameObject("CharacterView");
                    go.transform.SetParent(transform, false);
                    _characterViewRoot = go.transform;
                }
                else _characterViewRoot = view;
            }

            // Ensure CharacterView transform calibration & active state
            if (_characterViewRoot != null)
            {
                _characterViewRoot.localPosition = new Vector3(0f, groundOffset > 0f ? groundOffset : 0.90f, 0f);
                float scale = visualScale > 0f ? visualScale : 1.8361f;
                _characterViewRoot.localScale = new Vector3(scale, scale, scale);
                _characterViewRoot.localRotation = Quaternion.identity;
                _characterViewRoot.gameObject.SetActive(true);
                _characterViewRoot.gameObject.layer = 0;

                // Clean up legacy FBX child if present
                var oldFbx = _characterViewRoot.Find("FinalPlayerModel");
                if (oldFbx != null)
                {
                    if (Application.isPlaying) Destroy(oldFbx.gameObject);
                    else DestroyImmediate(oldFbx.gameObject);
                }

                // Ensure DarkNecromancer_GLB child under CharacterView
                var modelTransform = _characterViewRoot.Find("DarkNecromancer_GLB");
                if (modelTransform == null)
                {
                    var modelGo = new GameObject("DarkNecromancer_GLB");
                    modelGo.transform.SetParent(_characterViewRoot, false);
                    modelTransform = modelGo.transform;
                }

                modelTransform.localPosition = Vector3.zero;
                modelTransform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                modelTransform.localScale = Vector3.one;
                modelTransform.gameObject.layer = 0;
                modelTransform.gameObject.SetActive(true);

                var mf = modelTransform.GetComponent<MeshFilter>() ?? modelTransform.gameObject.AddComponent<MeshFilter>();
                var mr = modelTransform.GetComponent<MeshRenderer>() ?? modelTransform.gameObject.AddComponent<MeshRenderer>();

                // Ensure Mesh is bound
                bool hasValidMesh = false;
                try
                {
                    hasValidMesh = mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0;
                }
                catch
                {
                    hasValidMesh = false;
                }

                if (!hasValidMesh)
                {
                    // 1. Direct runtime binary loader (instant, 100% reliable 1.33M vertex mesh from authoritative GLB)
                    var binMesh = RuntimeDarkNecromancerMeshLoader.GetOrLoadMesh();
                    if (binMesh != null && binMesh.vertexCount > 0)
                    {
                        mf.sharedMesh = binMesh;
                        hasValidMesh = true;
                    }

#if UNITY_EDITOR
                    if (!hasValidMesh)
                    {
                        var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/player-model/DarkNecromancer.obj");
                        if (allAssets != null)
                        {
                            foreach (var a in allAssets)
                            {
                                if (a is Mesh m && m.vertexCount > 0)
                                {
                                    mf.sharedMesh = m;
                                    hasValidMesh = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (!hasValidMesh)
                    {
                        var modelObj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/player-model/DarkNecromancer.obj");
                        if (modelObj != null)
                        {
                            var objMf = modelObj.GetComponentInChildren<MeshFilter>();
                            if (objMf != null && objMf.sharedMesh != null && objMf.sharedMesh.vertexCount > 0)
                            {
                                mf.sharedMesh = objMf.sharedMesh;
                                hasValidMesh = true;
                            }
                        }
                    }
#endif
                    if (!hasValidMesh)
                    {
                        var prefabGo = Resources.Load<GameObject>("PlayerCharacter_Prefab");
                        if (prefabGo != null)
                        {
                            var prefabMf = prefabGo.GetComponentInChildren<MeshFilter>();
                            if (prefabMf != null && prefabMf.sharedMesh != null && prefabMf.sharedMesh.vertexCount > 0)
                            {
                                mf.sharedMesh = prefabMf.sharedMesh;
                                hasValidMesh = true;
                            }
                        }
                    }

                    Debug.Log($"[PlayerCharacter] '{gameObject.name}' visual mesh bound: {(mf.sharedMesh != null ? $"{mf.sharedMesh.name} ({mf.sharedMesh.vertexCount:N0} verts)" : "NULL")}");
                }

                // Ensure Material is bound
                bool hasValidMaterial = false;
                try
                {
                    hasValidMaterial = mr.sharedMaterial != null && mr.sharedMaterial.shader != null && !mr.sharedMaterial.shader.name.Contains("InternalErrorShader");
                }
                catch
                {
                    hasValidMaterial = false;
                }

                if (!hasValidMaterial)
                {
#if UNITY_EDITOR
                    var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/player-model/M_DarkNecromancer.mat");
                    if (mat != null)
                    {
                        mr.sharedMaterial = mat;
                        hasValidMaterial = true;
                    }
#endif
                    if (!hasValidMaterial)
                    {
                        var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        var fallbackMat = new Material(litShader) { name = "M_DarkNecromancer" };
#if UNITY_EDITOR
                        var baseTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/player-model/textures/necromancer_basecolor.jpeg");
                        if (baseTex != null)
                        {
                            fallbackMat.SetTexture("_BaseMap", baseTex);
                            fallbackMat.SetTexture("_MainTex", baseTex);
                        }
#endif
                        mr.sharedMaterial = fallbackMat;
                    }
                }

                mr.enabled = true;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
                _modelRenderer = mr;

                Debug.Log($"[PlayerCharacter] '{gameObject.name}' visual verified:\n" +
                    $"  Mesh: {(mf.sharedMesh != null ? $"{mf.sharedMesh.name} ({mf.sharedMesh.vertexCount:N0} verts)" : "NULL")}\n" +
                    $"  Material: {(mr.sharedMaterial != null ? mr.sharedMaterial.name : "NULL")}\n" +
                    $"  Renderer Enabled: {mr.enabled}\n" +
                    $"  Active In Hierarchy: {gameObject.activeInHierarchy}\n" +
                    $"  World Position: {transform.position}");
            }

            if (_effectsRoot == null)
            {
                var fx = transform.Find("Effects");
                if (fx == null)
                {
                    var go = new GameObject("Effects");
                    go.transform.SetParent(transform, false);
                    _effectsRoot = go.transform;
                }
                else _effectsRoot = fx;
            }

            if (_targetRingRoot == null)
            {
                var ring = transform.Find("TargetRing") ?? transform.Find("TargetIndicator");
                if (ring == null)
                {
                    var go = new GameObject("TargetRing");
                    go.transform.SetParent(transform, false);
                    _targetRingRoot = go.transform;
                }
                else _targetRingRoot = ring;
            }
            if (_targetRingVisual == null && _targetRingRoot != null)
            {
                _targetRingVisual = _targetRingRoot.gameObject;
            }

            if (_uiAnchorRoot == null)
            {
                var ui = transform.Find("UIAnchor");
                if (ui == null)
                {
                    var go = new GameObject("UIAnchor");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, targetHeight + 0.20f, 0f);
                    _uiAnchorRoot = go.transform;
                }
                else _uiAnchorRoot = ui;
            }

            if (_hitbox == null)
            {
                _hitbox = GetComponentInChildren<OpponentHitbox>();
                if (_hitbox == null)
                {
                    var hitboxGo = new GameObject("Hitbox");
                    hitboxGo.transform.SetParent(transform, false);
                    _hitbox = hitboxGo.AddComponent<OpponentHitbox>();
                }
            }
            if (_hitbox != null)
            {
                _hitbox.hitboxRadius = 0.5f;
                _hitbox.hitboxHeight = targetHeight;
                _hitbox.centerOffset = new Vector3(0f, targetHeight * 0.5f, 0f);
                _hitbox.showDebugHitbox = false;
            }

            if (_playerHitbox == null)
            {
                _playerHitbox = GetComponentInChildren<PlayerHitbox>();
                if (_playerHitbox == null && _hitbox != null)
                {
                    _playerHitbox = _hitbox.gameObject.AddComponent<PlayerHitbox>();
                }
            }
            if (_playerHitbox != null)
            {
                _playerHitbox.hitboxRadius = 0.5f;
                _playerHitbox.hitboxHeight = targetHeight;
                _playerHitbox.centerOffset = new Vector3(0f, targetHeight * 0.5f, 0f);
                _playerHitbox.showDebugHitbox = false;
            }

            if (_modelRenderer == null && _characterViewRoot != null)
            {
                _modelRenderer = _characterViewRoot.GetComponentInChildren<Renderer>();
            }
        }
    }
}
