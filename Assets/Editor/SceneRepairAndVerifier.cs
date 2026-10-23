using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArGuildEncounter.AR;
using ArGuildEncounter.DebugSuite;
using ArGuildEncounter.UI;

namespace ArGuildEncounter.EditorTools
{
    public static class SceneRepairAndVerifier
    {
        private const string ScenePath = "Assets/Scenes/Battle_DebugSimulation.unity";

        [MenuItem("Tools/AR Guild Encounter/Repair and Verify Battle Scene")]
        public static void RepairAndVerify()
        {
            Debug.Log("==================================================================");
            Debug.Log("STARTING SCENE INTEGRITY CHECK & REPAIR FOR BATTLE_DEBUGSIMULATION");
            Debug.Log("==================================================================");

            // Step 1: Verify Script Assets and Compilation
            Debug.Log("[STEP 1] Validating MonoScript Assets and Types...");
            var hitboxScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/AR/OpponentHitbox.cs");
            var runnerScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Debug/GameFoundationRunner.cs");
            var hudScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/UI/BattleHUDController.cs");

            if (hitboxScript == null || hitboxScript.GetClass() != typeof(OpponentHitbox))
            {
                Debug.LogError($"CRITICAL: OpponentHitbox script invalid or not compiled! Type: {hitboxScript?.GetClass()}");
                ExitIfBatchMode(1);
                return;
            }
            Debug.Log($"-> OpponentHitbox verified: {hitboxScript.GetClass().FullName} (GUID: {AssetDatabase.AssetPathToGUID("Assets/Scripts/AR/OpponentHitbox.cs")})");

            if (runnerScript == null || runnerScript.GetClass() != typeof(GameFoundationRunner))
            {
                Debug.LogError($"CRITICAL: GameFoundationRunner script invalid or not compiled! Type: {runnerScript?.GetClass()}");
                ExitIfBatchMode(1);
                return;
            }
            Debug.Log($"-> GameFoundationRunner verified: {runnerScript.GetClass().FullName} (GUID: {AssetDatabase.AssetPathToGUID("Assets/Scripts/Debug/GameFoundationRunner.cs")})");

            if (hudScript == null || hudScript.GetClass() != typeof(BattleHUDController))
            {
                Debug.LogError($"CRITICAL: BattleHUDController script invalid or not compiled! Type: {hudScript?.GetClass()}");
                ExitIfBatchMode(1);
                return;
            }
            Debug.Log($"-> BattleHUDController verified: {hudScript.GetClass().FullName} (GUID: {AssetDatabase.AssetPathToGUID("Assets/Scripts/UI/BattleHUDController.cs")})");

            // Step 2: Open Scene
            Debug.Log($"[STEP 2] Opening scene at {ScenePath}...");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"CRITICAL: Failed to open scene at {ScenePath}");
                ExitIfBatchMode(1);
                return;
            }

            // Step 3: Inspect and Repair Opponent_Target_Hitbox
            Debug.Log("[STEP 3] Inspecting & Repairing 'Opponent_Target_Hitbox'...");
            var hitboxGo = GameObject.Find("Opponent_Target_Hitbox");
            if (hitboxGo == null)
            {
                Debug.LogError("CRITICAL: GameObject 'Opponent_Target_Hitbox' not found in scene!");
                ExitIfBatchMode(1);
                return;
            }

            int missingOnHitbox = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(hitboxGo);
            Debug.Log($"-> Missing scripts on Opponent_Target_Hitbox before repair: {missingOnHitbox}");
            if (missingOnHitbox > 0)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(hitboxGo);
                Debug.Log($"-> Removed {removed} missing script component(s) from Opponent_Target_Hitbox.");
            }

            var hitboxComponent = hitboxGo.GetComponent<OpponentHitbox>();
            if (hitboxComponent == null)
            {
                hitboxComponent = hitboxGo.AddComponent<OpponentHitbox>();
                Debug.Log("-> Added OpponentHitbox component to Opponent_Target_Hitbox.");
            }
            hitboxComponent.hitboxRadius = 0.5f;
            hitboxComponent.hitboxHeight = 1.8f;
            hitboxComponent.centerOffset = new Vector3(0f, 0.9f, 0f);
            hitboxComponent.isTargetLocked = false;
            hitboxComponent.EnsureVisualMeshProxy();
            EditorUtility.SetDirty(hitboxGo);

            // Step 4: Inspect and Repair Battle_Coordinator_Manager
            Debug.Log("[STEP 4] Inspecting & Repairing 'Battle_Coordinator_Manager'...");
            var coordinatorGo = GameObject.Find("Battle_Coordinator_Manager");
            if (coordinatorGo == null)
            {
                Debug.LogError("CRITICAL: GameObject 'Battle_Coordinator_Manager' not found in scene!");
                ExitIfBatchMode(1);
                return;
            }

            int missingOnCoord = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(coordinatorGo);
            Debug.Log($"-> Missing scripts on Battle_Coordinator_Manager before repair: {missingOnCoord}");
            if (missingOnCoord > 0)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(coordinatorGo);
                Debug.Log($"-> Removed {removed} missing script component(s) from Battle_Coordinator_Manager.");
            }

            var hudComponent = coordinatorGo.GetComponent<BattleHUDController>();
            if (hudComponent == null)
            {
                hudComponent = coordinatorGo.AddComponent<BattleHUDController>();
                Debug.Log("-> Restored BattleHUDController component on Battle_Coordinator_Manager.");
            }

            var runnerComponent = coordinatorGo.GetComponent<GameFoundationRunner>();
            if (runnerComponent == null)
            {
                runnerComponent = coordinatorGo.AddComponent<GameFoundationRunner>();
                Debug.Log("-> Restored GameFoundationRunner component on Battle_Coordinator_Manager.");
            }

            // Build full visual Canvas, EventSystem, and all UI panels/controls
            Debug.Log("-> Generating visual Battle HUD (Canvas, EventSystem, Vitals, Banner, Spell Slots, Reticle, Log, Controls)...");
            hudComponent.EnsureVisualHUD();
            runnerComponent.hudController = hudComponent;
            runnerComponent.runAutomatedVerticalSliceOnStart = true;
            EditorUtility.SetDirty(coordinatorGo);

            // Step 5: Scene-Wide Missing Script Verification
            Debug.Log("[STEP 5] Performing scene-wide missing script verification...");
            int totalMissingInScene = 0;
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                foreach (var go in rootGo.GetComponentsInChildren<Transform>(true))
                {
                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject);
                    if (missingCount > 0)
                    {
                        Debug.LogError($"FAIL: GameObject '{go.gameObject.name}' still has {missingCount} missing script(s)!");
                        totalMissingInScene += missingCount;
                    }
                }
            }

            if (totalMissingInScene > 0)
            {
                Debug.LogError($"CRITICAL: Scene verification failed! Found {totalMissingInScene} missing script(s).");
                ExitIfBatchMode(1);
                return;
            }
            Debug.Log("-> Scene-wide check passed: 0 missing scripts across all GameObjects!");

            // Step 6: Save Scene
            Debug.Log($"[STEP 6] Saving repaired scene to {ScenePath}...");
            bool saveOk = EditorSceneManager.SaveScene(scene);
            if (!saveOk)
            {
                Debug.LogError("CRITICAL: Failed to save scene!");
                ExitIfBatchMode(1);
                return;
            }
            Debug.Log("-> Scene successfully saved by Unity Editor.");

            // Step 7: Test In-Scene Execution
            Debug.Log("[STEP 7] Simulating in-scene component initialization and vertical slice execution...");
            bool sliceSuccess = IntegratedVerticalSliceHarness.RunVerticalSlice(msg => Debug.Log($"[IN-SCENE SLICE] {msg}"));
            if (!sliceSuccess)
            {
                Debug.LogError("CRITICAL: In-scene vertical slice execution failed!");
                ExitIfBatchMode(1);
                return;
            }
            Debug.Log("-> In-scene simulation executed successfully!");

            Debug.Log("==================================================================");
            Debug.Log("SCENE REPAIR & INTEGRITY VERIFICATION COMPLETED WITH 100% SUCCESS");
            Debug.Log("==================================================================");
            ExitIfBatchMode(0);
        }

        private static void ExitIfBatchMode(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
