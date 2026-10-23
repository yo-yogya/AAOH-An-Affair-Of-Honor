using System;
using ArGuildEncounter.DebugSuite;

namespace VerificationTests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("==================================================================");
            Console.WriteLine("AR GUILD ENCOUNTER - FOUNDATION TEST SUITE RUNNER");
            Console.WriteLine("==================================================================");

            bool allPassed = true;

            Console.WriteLine("\n[SUITE 1] Verifying Battle State Machine Transitions...");
            bool smPassed = StateMachineVerification.RunVerification(msg => Console.WriteLine("  " + msg));
            if (!smPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: State Machine Verification!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: State Machine Verification!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 2] Verifying Combat Framework (HP, Mana, 7.5% Miss, Bluff, Turn)...");
            bool combatPassed = CombatFrameworkVerification.RunVerification(msg => Console.WriteLine("  " + msg));
            if (!combatPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Combat Framework Verification!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Combat Framework Verification!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 3] Verifying End-to-End Vertical Slice Flow...");
            bool slicePassed = IntegratedVerticalSliceHarness.RunVerticalSlice(msg => Console.WriteLine("  " + msg));
            if (!slicePassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Integrated Vertical Slice!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Integrated Vertical Slice!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 4] Verifying Scene Components & Scene File GUID Integrity...");
            bool sceneCheckPassed = true;
            try
            {
                // Verify Types & Inheritance
                if (!typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(typeof(ArGuildEncounter.AR.OpponentHitbox)))
                {
                    Console.WriteLine("  ERROR: OpponentHitbox is not a MonoBehaviour!");
                    sceneCheckPassed = false;
                }
                if (!typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(typeof(ArGuildEncounter.DebugSuite.GameFoundationRunner)))
                {
                    Console.WriteLine("  ERROR: GameFoundationRunner is not a MonoBehaviour!");
                    sceneCheckPassed = false;
                }
                if (!typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(typeof(ArGuildEncounter.UI.BattleHUDController)))
                {
                    Console.WriteLine("  ERROR: BattleHUDController is not a MonoBehaviour!");
                    sceneCheckPassed = false;
                }

                // Verify Scene YAML GUID references
                string sceneFilePath = System.IO.Path.Combine("Assets", "Scenes", "Battle_DebugSimulation.unity");
                if (!System.IO.File.Exists(sceneFilePath))
                {
                    sceneFilePath = System.IO.Path.Combine("..", "Assets", "Scenes", "Battle_DebugSimulation.unity");
                }
                if (System.IO.File.Exists(sceneFilePath))
                {
                    string sceneContent = System.IO.File.ReadAllText(sceneFilePath);
                    if (sceneContent.Contains("00000000000000000000000000000001") ||
                        sceneContent.Contains("00000000000000000000000000000002") ||
                        sceneContent.Contains("00000000000000000000000000000003"))
                    {
                        Console.WriteLine("  ERROR: Scene still contains dummy GUIDs (0000...0001/2/3)!");
                        sceneCheckPassed = false;
                    }

                    // Check for real script GUIDs
                    if (!sceneContent.Contains("7502342509b682244a8ee0ce44e9a554"))
                    {
                        Console.WriteLine("  ERROR: Scene missing OpponentHitbox GUID (7502342509b682244a8ee0ce44e9a554)!");
                        sceneCheckPassed = false;
                    }
                    if (!sceneContent.Contains("a1d1360ed5932364ca0072b1092cfb33"))
                    {
                        Console.WriteLine("  ERROR: Scene missing GameFoundationRunner GUID (a1d1360ed5932364ca0072b1092cfb33)!");
                        sceneCheckPassed = false;
                    }
                    if (!sceneContent.Contains("e7e094ed5b70bd4448f8c90fa1235e13"))
                    {
                        Console.WriteLine("  ERROR: Scene missing BattleHUDController GUID (e7e094ed5b70bd4448f8c90fa1235e13)!");
                        sceneCheckPassed = false;
                    }

                    // Verify BattleHUDController SpellSlotCount contract & scene serialization
                    int spellSlotCount = (int)typeof(ArGuildEncounter.UI.BattleHUDController).GetField("SpellSlotCount").GetValue(null);
                    if (spellSlotCount != 6)
                    {
                        Console.WriteLine($"  ERROR: BattleHUDController.SpellSlotCount is {spellSlotCount}, expected 6!");
                        sceneCheckPassed = false;
                    }
                    if (sceneContent.Contains("spellSlotButtons: []") || sceneContent.Contains("spellSlotTexts: []"))
                    {
                        Console.WriteLine("  ERROR: Scene still serializes spellSlotButtons or spellSlotTexts as empty arrays []!");
                        sceneCheckPassed = false;
                    }
                }
                else
                {
                    Console.WriteLine($"  ERROR: Scene file not found at {sceneFilePath}!");
                    sceneCheckPassed = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  EXCEPTION: {ex.Message}");
                sceneCheckPassed = false;
            }

            if (!sceneCheckPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Scene Components & GUID Integrity!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Scene Components & GUID Integrity!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 5] Verifying Authoritative Combat Balance (12 Scenarios)...");
            bool balancePassed = AuthoritativeBalanceVerification.RunAllScenarios(msg => Console.WriteLine("  " + msg));
            if (!balancePassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Authoritative Combat Balance (12 Scenarios)!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Authoritative Combat Balance (12 Scenarios)!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 6] Verifying Authoritative State Sync & Full Reset...");
            bool syncPassed = AuthoritativeSyncVerification.RunAllTests(msg => Console.WriteLine("  " + msg));
            if (!syncPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Authoritative State Sync & Full Reset!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Authoritative State Sync & Full Reset!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 7] Verifying Gameplay Integrity & Interaction Architecture...");
            bool integrityPassed = GameplayIntegrityVerification.RunAllTests(msg => Console.WriteLine("  " + msg));
            if (!integrityPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Gameplay Integrity & Interaction Architecture!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Gameplay Integrity & Interaction Architecture!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 8] Verifying Two-Device AR PvP, Dual Hitbox, Spatial Proxy & Calibration...");
            bool pvpPassed = TwoDevicePvPVerification.RunVerification(msg => Console.WriteLine("  " + msg));
            if (!pvpPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Two-Device AR PvP & Dual Hitbox Verification!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Two-Device AR PvP & Dual Hitbox Verification!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 9] Generating and Verifying ScriptableObject Data Assets (30 Spells, 5 Guilds, 10 Terrains)...");
            string rootPath = System.IO.Directory.Exists("Assets") ? "." : "..";
            bool assetsPassed = DataAssetGenerator.GenerateAndValidate(rootPath, msg => Console.WriteLine("  " + msg));
            if (!assetsPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Data Asset Generation & Validation!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Data Asset Generation & Validation!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 10] Verifying Autonomous Game Systems (Arena, Character, Targeting, Gesture, CPU)...");
            bool systemsPassed = AutonomousGameSystemsVerification.RunVerification(msg => Console.WriteLine("  " + msg));
            if (!systemsPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Autonomous Game Systems Verification!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Autonomous Game Systems Verification!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 11] Verifying Top-Level Match Flow & Main Menu Navigation...");
            bool matchFlowPassed = MatchFlowVerification.RunVerification(msg => Console.WriteLine("  " + msg));
            if (!matchFlowPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Top-Level Match Flow & Main Menu Navigation!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Top-Level Match Flow & Main Menu Navigation!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 12] Verifying Player Model Import, Calibration, Grounding & Scaling...");
            bool modelPassed = PlayerModelVerification.RunVerification(msg => Console.WriteLine("  " + msg));
            if (!modelPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Player Model Import & Calibration Verification!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Player Model Import & Calibration Verification!");
                Console.ResetColor();
            }

            Console.WriteLine("\n[SUITE 13] Verifying Path Selection Phase (Authoritative Sync, Stat Payloads, Fallback & Transitions)...");
            bool pathSelectionPassed = PathSelectionVerification.RunVerification(msg => Console.WriteLine("  " + msg));
            if (!pathSelectionPassed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Path Selection Verification!");
                Console.ResetColor();
                allPassed = false;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Path Selection Verification!");
                Console.ResetColor();
            }

            Console.WriteLine("\n==================================================================");
            if (allPassed)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("ALL AR GUILD ENCOUNTER VERIFICATION SUITES PASSED (100% SUCCESS)");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ONE OR MORE SUITES FAILED!");
                Console.ResetColor();
                return 1;
            }
        }
    }
}
