#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArGuildEncounter.AR;
using ArGuildEncounter.Players;
using ArGuildEncounter.DebugSuite;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.EditorTools
{
    /// <summary>
    /// Authoritative importer and setup tool for Dark Necromancer GLB model.
    /// Replaces previous FBX model completely with dark_necromancer_-_corrupted_staff__skulls.glb.
    /// </summary>
    [InitializeOnLoad]
    public static class DarkNecromancerGLBImporter
    {
        public const string GlbPath = "Assets/player-model/dark_necromancer_-_corrupted_staff__skulls.glb";
        public const string MeshBinPath = "Assets/player-model/dark_necromancer_mesh.bin";
        public const string MeshAssetPath = "Assets/player-model/DarkNecromancer_Mesh.asset";
        public const string ModelObjPath = "Assets/player-model/DarkNecromancer.obj";
        public const string MaterialAssetPath = "Assets/player-model/M_DarkNecromancer.mat";
        public const string VisualPrefabPath = "Assets/player-model/DarkNecromancer_GLB.prefab";
        public const string BaseColorTexPath = "Assets/player-model/textures/necromancer_basecolor.jpeg";
        public const string NormalTexPath = "Assets/player-model/textures/necromancer_normal.png";
        public const string PrefabPath = "Assets/Prefabs/PlayerCharacter_Prefab.prefab";
        public const string ResourcesPrefabPath = "Assets/Resources/PlayerCharacter_Prefab.prefab";
        public const string OpponentPrefabPath = "Assets/Prefabs/OpponentCharacter_Prefab.prefab";
        public const string BattleScenePath = "Assets/Scenes/Battle_DebugSimulation.unity";

        public const float TargetHeight = 1.80f;
        public const float CalibratedScale = 1.8361f;
        public const float GroundYOffset = 0.90f;

        static DarkNecromancerGLBImporter()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // Check if mesh already exists
            if (!File.Exists(MeshAssetPath) || !File.Exists(MaterialAssetPath) || !File.Exists(VisualPrefabPath))
            {
                Debug.Log("[DARK NECROMANCER GLB] Running automated first-time import...");
                ImportAndConfigureAll();
            }
        }

        [MenuItem("Tools/AR Guild Encounter/Import and Configure Dark Necromancer GLB")]
        public static void ImportAndConfigureAll()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[DARK NECROMANCER GLB] Cannot import during Play Mode.");
                return;
            }

            Debug.Log("==================================================================");
            Debug.Log("[DARK NECROMANCER GLB] Starting Authoritative GLB Model Pipeline...");
            Debug.Log("==================================================================");

            // Step 1: Ensure Textures and Import Settings
            ConfigureTextures();

            // Step 2: Ensure Material
            Material mat = EnsureMaterial();

            // Step 3: Ensure Mesh Asset
            Mesh mesh = EnsureMesh();
            if (mesh == null)
            {
                Debug.LogError("[DARK NECROMANCER GLB] Failed to create or load mesh!");
                return;
            }

            // Step 4: Ensure Visual Prefab
            EnsureVisualPrefab(mesh, mat);

            // Step 5: Configure PlayerCharacter Prefabs
            ConfigurePlayerPrefab(PrefabPath, mesh, mat);
            ConfigurePlayerPrefab(ResourcesPrefabPath, mesh, mat);
            if (File.Exists(OpponentPrefabPath))
            {
                ConfigurePlayerPrefab(OpponentPrefabPath, mesh, mat);
            }

            // Step 6: Configure Battle Scene
            SetupBattleScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DARK NECROMANCER GLB] Pipeline Execution COMPLETE!");
        }

        private static void ConfigureTextures()
        {
            // Normal Map
            if (File.Exists(NormalTexPath))
            {
                var normalImporter = AssetImporter.GetAtPath(NormalTexPath) as TextureImporter;
                if (normalImporter != null && (normalImporter.textureType != TextureImporterType.NormalMap || normalImporter.sRGBTexture))
                {
                    normalImporter.textureType = TextureImporterType.NormalMap;
                    normalImporter.sRGBTexture = false;
                    normalImporter.SaveAndReimport();
                    Debug.Log("[DARK NECROMANCER GLB] Configured necromancer_normal.png as NormalMap (sRGB = false).");
                }
            }

            // BaseColor Map
            if (File.Exists(BaseColorTexPath))
            {
                var baseImporter = AssetImporter.GetAtPath(BaseColorTexPath) as TextureImporter;
                if (baseImporter != null && !baseImporter.sRGBTexture)
                {
                    baseImporter.textureType = TextureImporterType.Default;
                    baseImporter.sRGBTexture = true;
                    baseImporter.SaveAndReimport();
                    Debug.Log("[DARK NECROMANCER GLB] Configured necromancer_basecolor.jpeg as sRGB BaseMap.");
                }
            }
        }

        private static Material EnsureMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(litShader) { name = "M_DarkNecromancer" };
                AssetDatabase.CreateAsset(mat, MaterialAssetPath);
                Debug.Log($"[DARK NECROMANCER GLB] Created new material asset at {MaterialAssetPath}.");
            }
            else
            {
                mat.shader = litShader;
            }

            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorTexPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTexPath);

            if (baseTex != null)
            {
                mat.SetTexture("_BaseMap", baseTex);
                mat.SetTexture("_MainTex", baseTex);
            }

            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Cull", 0f); // Double-sided (0 = Off)
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0.10f);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh EnsureMesh()
        {
            // 1. First check if native Mesh from DarkNecromancer.obj is available (1st class Unity model asset)
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(ModelObjPath);
            if (allAssets != null)
            {
                foreach (var asset in allAssets)
                {
                    if (asset is Mesh m && m.vertexCount > 0)
                    {
                        Debug.Log($"[DARK NECROMANCER GLB] Loaded native model Mesh from {ModelObjPath} subassets ({m.name}, {m.vertexCount:N0} verts).");
                        return m;
                    }
                }
            }

            var meshFromObj = AssetDatabase.LoadAssetAtPath<Mesh>(ModelObjPath);
            if (meshFromObj != null && meshFromObj.vertexCount > 0)
            {
                Debug.Log($"[DARK NECROMANCER GLB] Loaded native model Mesh from {ModelObjPath} ({meshFromObj.vertexCount:N0} verts).");
                return meshFromObj;
            }

            var modelObj = AssetDatabase.LoadAssetAtPath<GameObject>(ModelObjPath);
            if (modelObj != null)
            {
                var mf = modelObj.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0)
                {
                    Debug.Log($"[DARK NECROMANCER GLB] Loaded native model Mesh from {ModelObjPath} child ({mf.sharedMesh.vertexCount:N0} verts).");
                    return mf.sharedMesh;
                }
            }

            // 2. Direct runtime binary loader (guarantees mesh is NEVER null under any condition)
            var binMesh = RuntimeDarkNecromancerMeshLoader.GetOrLoadMesh();
            if (binMesh != null && binMesh.vertexCount > 0)
            {
                Debug.Log($"[DARK NECROMANCER GLB] Loaded Mesh from binary data ({binMesh.vertexCount:N0} verts).");
                return binMesh;
            }

            Debug.LogError($"[DARK NECROMANCER GLB] Could not load mesh from {ModelObjPath} or binary data!");
            return null;
        }

        private static void EnsureVisualPrefab(Mesh mesh, Material mat)
        {
            var go = new GameObject("DarkNecromancer_GLB");
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            go.transform.localScale = Vector3.one;

            PrefabUtility.SaveAsPrefabAsset(go, VisualPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[DARK NECROMANCER GLB] Created visual prefab at {VisualPrefabPath}.");
        }

        private static void ConfigurePlayerPrefab(string prefabPath, Mesh mesh, Material mat)
        {
            if (!File.Exists(prefabPath)) return;

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // A. Ensure GameplayRoot child
                var gameplayRoot = prefabRoot.transform.Find("GameplayRoot");
                if (gameplayRoot == null)
                {
                    var gpGo = new GameObject("GameplayRoot");
                    gpGo.transform.SetParent(prefabRoot.transform, false);
                    gameplayRoot = gpGo.transform;
                }
                gameplayRoot.localPosition = Vector3.zero;
                gameplayRoot.localRotation = Quaternion.identity;
                gameplayRoot.localScale = Vector3.one;

                // B. Ensure CharacterView child
                var charView = prefabRoot.transform.Find("CharacterView");
                if (charView == null)
                {
                    var viewGo = new GameObject("CharacterView");
                    viewGo.transform.SetParent(prefabRoot.transform, false);
                    charView = viewGo.transform;
                }
                charView.localPosition = new Vector3(0f, GroundYOffset, 0f);
                charView.localRotation = Quaternion.identity;
                charView.localScale = new Vector3(CalibratedScale, CalibratedScale, CalibratedScale);

                // Clean legacy FBX / placeholder children
                var oldFbx = charView.Find("FinalPlayerModel");
                if (oldFbx != null) UnityEngine.Object.DestroyImmediate(oldFbx.gameObject);
                var oldFilter = charView.GetComponent<MeshFilter>();
                if (oldFilter != null) UnityEngine.Object.DestroyImmediate(oldFilter);
                var oldRenderer = charView.GetComponent<MeshRenderer>();
                if (oldRenderer != null) UnityEngine.Object.DestroyImmediate(oldRenderer);

                // C. Ensure DarkNecromancer_GLB child under CharacterView
                var glbChild = charView.Find("DarkNecromancer_GLB");
                if (glbChild == null)
                {
                    var glbGo = new GameObject("DarkNecromancer_GLB");
                    glbGo.transform.SetParent(charView, false);
                    glbChild = glbGo.transform;
                }
                glbChild.localPosition = Vector3.zero;
                glbChild.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                glbChild.localScale = Vector3.one;

                var filter = glbChild.GetComponent<MeshFilter>() ?? glbChild.gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;

                var renderer = glbChild.GetComponent<MeshRenderer>() ?? glbChild.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;

                // D. Root PlayerCharacter component
                var playerCharacter = prefabRoot.GetComponent<PlayerCharacter>() ?? prefabRoot.AddComponent<PlayerCharacter>();
                playerCharacter.targetHeight = TargetHeight;
                playerCharacter.visualScale = CalibratedScale;
                playerCharacter.groundOffset = GroundYOffset;

                // E. Calibrate Hitbox CapsuleCollider on root
                var col = prefabRoot.GetComponent<CapsuleCollider>() ?? prefabRoot.AddComponent<CapsuleCollider>();
                col.radius = 0.5f;
                col.height = TargetHeight;
                col.center = new Vector3(0f, TargetHeight * 0.5f, 0f);
                col.isTrigger = true;

                // F. Calibrate Hitbox child
                var hitboxChild = prefabRoot.transform.Find("Hitbox");
                if (hitboxChild != null)
                {
                    var oppHitbox = hitboxChild.GetComponent<OpponentHitbox>() ?? hitboxChild.gameObject.AddComponent<OpponentHitbox>();
                    oppHitbox.hitboxHeight = TargetHeight;
                    oppHitbox.hitboxRadius = 0.5f;
                    oppHitbox.centerOffset = new Vector3(0f, TargetHeight * 0.5f, 0f);
                    oppHitbox.showDebugHitbox = false;

                    var playerHitbox = hitboxChild.GetComponent<PlayerHitbox>() ?? hitboxChild.gameObject.AddComponent<PlayerHitbox>();
                    playerHitbox.hitboxHeight = TargetHeight;
                    playerHitbox.hitboxRadius = 0.5f;
                    playerHitbox.centerOffset = new Vector3(0f, TargetHeight * 0.5f, 0f);
                    playerHitbox.showDebugHitbox = false;
                }

                // G. Calibrate TargetRing
                var targetRing = prefabRoot.transform.Find("TargetRing") ?? prefabRoot.transform.Find("TargetIndicator");
                if (targetRing != null)
                {
                    targetRing.name = "TargetRing";
                    targetRing.localPosition = new Vector3(0f, 0.02f, 0f);
                    targetRing.localScale = new Vector3(1.2f, 0.02f, 1.2f);
                }

                // H. Calibrate UIAnchor
                var uiAnchor = prefabRoot.transform.Find("UIAnchor");
                if (uiAnchor != null)
                {
                    uiAnchor.localPosition = new Vector3(0f, TargetHeight + 0.20f, 0f);
                }

                // I. Wire serialized properties on PlayerCharacter
                var so = new SerializedObject(playerCharacter);
                so.Update();
                so.FindProperty("_modelRenderer").objectReferenceValue = renderer;
                so.FindProperty("_characterViewRoot").objectReferenceValue = charView;
                so.FindProperty("_gameplayRoot").objectReferenceValue = gameplayRoot;
                so.FindProperty("_effectsRoot").objectReferenceValue = prefabRoot.transform.Find("Effects");
                so.FindProperty("_targetRingRoot").objectReferenceValue = targetRing;
                so.FindProperty("_uiAnchorRoot").objectReferenceValue = uiAnchor;
                if (hitboxChild != null)
                {
                    so.FindProperty("_hitbox").objectReferenceValue = hitboxChild.GetComponent<OpponentHitbox>();
                    so.FindProperty("_playerHitbox").objectReferenceValue = hitboxChild.GetComponent<PlayerHitbox>();
                }
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log($"[DARK NECROMANCER GLB] Configured prefab at {prefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public static void SetupBattleScene()
        {
            if (!File.Exists(BattleScenePath)) return;

            var activeScene = EditorSceneManager.GetActiveScene();
            var scene = (activeScene.IsValid() && activeScene.path == BattleScenePath)
                ? activeScene
                : EditorSceneManager.OpenScene(BattleScenePath);

            // Remove obsolete standalone Opponent_Target_Hitbox if present
            var oldHitbox = GameObject.Find("Opponent_Target_Hitbox");
            if (oldHitbox != null)
            {
                UnityEngine.Object.DestroyImmediate(oldHitbox);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;

            // 1. Local Player Instance (Spawned grounded at crosshair target)
            var localGo = GameObject.Find("PlayerCharacter_Local");
            if (localGo != null) UnityEngine.Object.DestroyImmediate(localGo);
            localGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            localGo.name = "PlayerCharacter_Local";
            localGo.transform.position = new Vector3(0f, 0f, 1.338f);
            localGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var localChar = localGo.GetComponent<PlayerCharacter>();
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            if (localChar != null) localChar.Initialize("Player_1", isLocal: true, isHost: true, magma);

            // 2. Remote Player Instance
            var remoteGo = GameObject.Find("PlayerCharacter_Remote");
            if (remoteGo != null) UnityEngine.Object.DestroyImmediate(remoteGo);
            remoteGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            remoteGo.name = "PlayerCharacter_Remote";
            remoteGo.transform.position = new Vector3(0f, 0f, 3.54f);
            remoteGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var remoteChar = remoteGo.GetComponent<PlayerCharacter>();
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            if (remoteChar != null) remoteChar.Initialize("Player_2", isLocal: false, isHost: false, aqua);

            // 3. Update GameFoundationRunner
            var runner = UnityEngine.Object.FindAnyObjectByType<GameFoundationRunner>();
            if (runner != null)
            {
                runner.playerCharacterPrefab = prefab;
                runner.localCharacter = localChar;
                runner.remoteCharacter = remoteChar;
                runner.localHitbox = localChar != null ? localChar.PlayerHitbox : null;
                runner.remoteHitbox = remoteChar != null ? remoteChar.PlayerHitbox : null;
                runner.startInMainMenu = false;
                EditorUtility.SetDirty(runner);
            }

            // 4. Set Duel Camera View
            var xrOrigin = GameObject.Find("XR Origin");
            if (xrOrigin != null)
            {
                xrOrigin.transform.position = new Vector3(0f, 1.65f, -2.20f);
                xrOrigin.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
                EditorUtility.SetDirty(xrOrigin);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DARK NECROMANCER GLB] Successfully updated Battle_DebugSimulation scene with Dark Necromancer model!");
        }
    }
}
#endif
