using System;
using UnityEngine;
using UnityEditor;

namespace ArGuildEncounter.DebugSuite
{
    public static class EditorTestRunner
    {
        [MenuItem("Tools/AR Guild Encounter/Run Foundation Tests")]
        public static void RunAllTests()
        {
            Debug.Log("=====================================================");
            Debug.Log("RUNNING AR GUILD ENCOUNTER BATCHMODE TEST SUITE");
            Debug.Log("=====================================================");

            try
            {
                bool stateMachineOk = StateMachineVerification.RunVerification(msg => Debug.Log(msg));
                if (!stateMachineOk)
                {
                    Debug.LogError("CRITICAL: State Machine Verification FAILED!");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                bool combatOk = CombatFrameworkVerification.RunVerification(msg => Debug.Log(msg));
                if (!combatOk)
                {
                    Debug.LogError("CRITICAL: Combat Framework Verification FAILED!");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                bool sliceOk = IntegratedVerticalSliceHarness.RunVerticalSlice(msg => Debug.Log(msg));
                if (!sliceOk)
                {
                    Debug.LogError("CRITICAL: Integrated Vertical Slice Verification FAILED!");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                bool modelOk = PlayerModelVerification.RunVerification(msg => Debug.Log(msg));
                if (!modelOk)
                {
                    Debug.LogError("CRITICAL: Player Model Verification FAILED!");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }

                Debug.Log("=====================================================");
                Debug.Log("ALL AR GUILD ENCOUNTER VERIFICATIONS PASSED (100% SUCCESS)");
                Debug.Log("=====================================================");

                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"CRITICAL EXCEPTION IN TESTS: {ex.Message}\n{ex.StackTrace}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
