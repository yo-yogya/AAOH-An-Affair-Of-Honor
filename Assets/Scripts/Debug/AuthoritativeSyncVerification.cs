using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Battle;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Environment;
using ArGuildEncounter.AR;
using ArGuildEncounter.Voice;
using ArGuildEncounter.Gesture;
using ArGuildEncounter.Networking;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Verification suite for authoritative single-source-of-truth state synchronization,
    /// dynamic spell voice and gesture controls, authoritative miss chance calculation,
    /// and full battle state reset.
    /// </summary>
    public static class AuthoritativeSyncVerification
    {
        public static bool RunAllTests(Action<string> log)
        {
            bool allPassed = true;

            log("--- [SYNC TEST 1] Verifying Single Source of Truth & Initial Turn Phase ---");
            if (!TestSingleSourceOfTruth(log)) allPassed = false;

            log("--- [SYNC TEST 2] Verifying Spell Selection & Active Player Loadout Enforcement ---");
            if (!TestLoadoutAndSpellSync(log)) allPassed = false;

            log("--- [SYNC TEST 3] Verifying Authoritative Miss Calculation (Never 0.0%) ---");
            if (!TestAuthoritativeMissCalculation(log)) allPassed = false;

            log("--- [SYNC TEST 4] Verifying Complete Battle Reset ---");
            if (!TestCompleteBattleReset(log)) allPassed = false;

            return allPassed;
        }

        private static bool TestSingleSourceOfTruth(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;

            var network = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var gesture = new MockGestureService(() => 0f);

            var coordinator = new BattleCoordinator(config, network, ar, voice, gesture, () => 0f);

            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var terrain = AuthoritativeCombatRoster.GetTerrain("VOLCANIC_RUINS");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Volcanic Ruins", null);

            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");

            coordinator.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena, terrain);
            coordinator.StartBattle(useDebugQuickStart: true);

            // Assertions
            if (coordinator.StateMachine.CurrentPhase != BattlePhase.SpellSelection)
            {
                log($"ERROR: Expected phase SpellSelection, got {coordinator.StateMachine.CurrentPhase}");
                return false;
            }

            if (coordinator.TurnManager.ActivePlayerId != "Player_1")
            {
                log($"ERROR: Expected ActivePlayerId Player_1, got {coordinator.TurnManager.ActivePlayerId}");
                return false;
            }

            if (coordinator.TurnManager.CurrentTurnNumber != 1)
            {
                log($"ERROR: Expected Turn 1, got {coordinator.TurnManager.CurrentTurnNumber}");
                return false;
            }

            if (coordinator.SelectedSpell != null)
            {
                log($"ERROR: Expected SelectedSpell to be null on turn start, got {coordinator.SelectedSpell.displayName}");
                return false;
            }

            log("  Single source of truth confirmed on battle start.");
            return true;
        }

        private static bool TestLoadoutAndSpellSync(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;

            var network = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var gesture = new MockGestureService(() => 0f);

            var coordinator = new BattleCoordinator(config, network, ar, voice, gesture, () => 0f);
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var terrain = AuthoritativeCombatRoster.GetTerrain("VOLCANIC_RUINS");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Volcanic Ruins", null);

            coordinator.SetupMatch("Player_1", magma, AuthoritativeCombatRoster.GetSampleLoadout("MAGMA"),
                                   "Player_2", aqua, AuthoritativeCombatRoster.GetSampleLoadout("AQUA"), arena, terrain);
            coordinator.StartBattle(useDebugQuickStart: true);

            // Attempt to summon opponent's spell "Quick Freeze" (should be rejected since Player 1 does not own it)
            voice.SimulateSpokenPhrase("Quick Freeze");
            if ((object)coordinator.SelectedSpell != null)
            {
                log("ERROR: Summoning opponent spell succeeded when it must be rejected!");
                return false;
            }

            // Summon legal spell "Fireball"
            voice.SimulateSpokenPhrase("Fireball");
            string dispName = coordinator.SelectedSpell?.displayName;
            bool isNull = (object)coordinator.SelectedSpell == null;
            bool equals = string.Equals(dispName?.Trim(), "Fireball", StringComparison.OrdinalIgnoreCase);
            if (isNull || !equals)
            {
                log($"ERROR: Summoning legal spell 'Fireball' failed! Selected: '{dispName ?? "null"}'");
                return false;
            }

            if (coordinator.StateMachine.CurrentPhase != BattlePhase.GestureWindow)
            {
                log($"ERROR: Phase should be GestureWindow, got {coordinator.StateMachine.CurrentPhase}");
                return false;
            }

            // Gesture validation
            string expectedGesture = coordinator.SelectedSpell.gestureId;
            gesture.SimulateGesturePerformed(expectedGesture, 0.95f);
            if (coordinator.StateMachine.CurrentPhase != BattlePhase.AimPhase)
            {
                log($"ERROR: Phase should be AimPhase after matching gesture, got {coordinator.StateMachine.CurrentPhase}");
                return false;
            }

            log("  Loadout validation, voice recognition, and dynamic gesture progression confirmed.");
            return true;
        }

        private static bool TestAuthoritativeMissCalculation(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            var pipeline = new AttackResolutionPipeline(config);

            var fireball = AuthoritativeCombatRoster.GetSpell("FIREBALL");
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");

            var attacker = new PlayerCombatState("P1", config, magma);
            attacker.SetLoadout(AuthoritativeCombatRoster.GetSampleLoadout("MAGMA"), AuthoritativeCombatRoster.GetSpell("STRUGGLE"));
            var defender = new PlayerCombatState("P2", config, aqua);
            defender.SetLoadout(AuthoritativeCombatRoster.GetSampleLoadout("AQUA"), AuthoritativeCombatRoster.GetSpell("STRUGGLE"));

            // 1. Gesture Failed (Cast Validation failure)
            var resFailedCast = pipeline.ResolveAttack(attacker, defender, fireball, null, null, false, true);
            if (resFailedCast.finalMissChance < 0.075f)
            {
                log($"ERROR: Failed cast returned finalMissChance {resFailedCast.finalMissChance * 100f}%, expected >= 7.5%!");
                return false;
            }

            // 2. Target Not Acquired
            var resFailedTarget = pipeline.ResolveAttack(attacker, defender, fireball, null, null, true, false);
            if (resFailedTarget.finalMissChance < 0.075f)
            {
                log($"ERROR: Failed target returned finalMissChance {resFailedTarget.finalMissChance * 100f}%, expected >= 7.5%!");
                return false;
            }

            // 3. Normal resolution with 0 modifiers
            var resNormal = pipeline.ResolveAttack(attacker, defender, fireball, null, null, true, true);
            if (Mathf.Abs(resNormal.finalMissChance - 0.075f) > 0.001f)
            {
                log($"ERROR: Normal resolution miss chance was {resNormal.finalMissChance * 100f}%, expected exactly 7.5%!");
                return false;
            }

            log("  Authoritative miss calculation is never 0.0% even on early validation exits.");
            return true;
        }

        private static bool TestCompleteBattleReset(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;

            var network = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var gesture = new MockGestureService(() => 0f);

            var coordinator = new BattleCoordinator(config, network, ar, voice, gesture, () => 0f);
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var terrain = AuthoritativeCombatRoster.GetTerrain("VOLCANIC_RUINS");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Volcanic Ruins", null);

            coordinator.SetupMatch("Player_1", magma, AuthoritativeCombatRoster.GetSampleLoadout("MAGMA"),
                                   "Player_2", aqua, AuthoritativeCombatRoster.GetSampleLoadout("AQUA"), arena, terrain);
            coordinator.StartBattle(useDebugQuickStart: true);

            // Mutate battle state
            coordinator.LocalPlayer.ApplyDamage(250);
            coordinator.LocalPlayer.TryDeductMana(40);
            coordinator.OpponentPlayer.ApplyDamage(300);
            coordinator.OpponentPlayer.TryDeductMana(20);
            coordinator.LocalPlayer.TryApplyMoveSkip();
            coordinator.ResolutionPipeline.ForceMissDebug = true;
            coordinator.ResolutionPipeline.ForceCritDebug = true;

            // Voice selection
            voice.SimulateSpokenPhrase("Fireball");

            // Reset match
            bool resetEventFired = false;
            coordinator.OnMatchReset += () => resetEventFired = true;
            coordinator.ResetMatch();

            if (!resetEventFired)
            {
                log("ERROR: OnMatchReset event was not fired!");
                return false;
            }

            if (coordinator.StateMachine.CurrentPhase != BattlePhase.WaitingForPlayer)
            {
                log($"ERROR: Phase after reset should be WaitingForPlayer, got {coordinator.StateMachine.CurrentPhase}");
                return false;
            }

            if (coordinator.TurnManager.CurrentTurnNumber != 0)
            {
                log($"ERROR: TurnManager.CurrentTurnNumber after reset should be 0, got {coordinator.TurnManager.CurrentTurnNumber}");
                return false;
            }

            if (coordinator.TurnManager.ActivePlayerId != null)
            {
                log($"ERROR: TurnManager.ActivePlayerId after reset should be null, got {coordinator.TurnManager.ActivePlayerId}");
                return false;
            }

            if (coordinator.SelectedSpell != null)
            {
                log($"ERROR: SelectedSpell after reset should be null, got {coordinator.SelectedSpell.displayName}");
                return false;
            }

            if (coordinator.LocalPlayer.CurrentHP != coordinator.LocalPlayer.MaxHP)
            {
                log($"ERROR: LocalPlayer HP not restored! HP: {coordinator.LocalPlayer.CurrentHP}/{coordinator.LocalPlayer.MaxHP}");
                return false;
            }

            if (coordinator.LocalPlayer.CurrentMana != coordinator.LocalPlayer.MaxMana)
            {
                log($"ERROR: LocalPlayer Mana not restored! Mana: {coordinator.LocalPlayer.CurrentMana}/{coordinator.LocalPlayer.MaxMana}");
                return false;
            }

            if (coordinator.LocalPlayer.ActiveStatuses.Count != 0 || coordinator.LocalPlayer.IsMoveSkipActive)
            {
                log("ERROR: LocalPlayer statuses/control states not cleared!");
                return false;
            }

            if (coordinator.OpponentPlayer.CurrentHP != coordinator.OpponentPlayer.MaxHP)
            {
                log($"ERROR: OpponentPlayer HP not restored!");
                return false;
            }

            if (coordinator.ResolutionPipeline.ForceMissDebug || coordinator.ResolutionPipeline.ForceCritDebug)
            {
                log("ERROR: ResolutionPipeline debug overrides not cleared!");
                return false;
            }

            log("  Complete match reset verified: all subsystems returned to clean WaitingForPlayer state.");
            return true;
        }
    }
}
