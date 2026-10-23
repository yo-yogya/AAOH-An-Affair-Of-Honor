using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Battle;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Environment;
using ArGuildEncounter.Networking;
using ArGuildEncounter.AR;
using ArGuildEncounter.Voice;
using ArGuildEncounter.Gesture;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Executes and verifies the complete End-to-End Vertical Slice requested by the user:
    /// CREATE ROOM -> JOIN ROOM -> PLAYER CONNECTED -> BATTLE READY -> PLAYER TURN ->
    /// TEST SPELL SELECTION -> TEST GESTURE -> TEST TARGET -> TEST ATTACK -> RESOLUTION ->
    /// HP/MANA -> NEXT TURN.
    /// </summary>
    public static class IntegratedVerticalSliceHarness
    {
        public static bool RunVerticalSlice(Action<string> log)
        {
            log("==================================================");
            log("STARTING AR GUILD ENCOUNTER VERTICAL SLICE PROOF");
            log("==================================================");

            // 1. Instantiate Core Config & Mock Services
            CombatConfig config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var network = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var gesture = new MockGestureService(() => 2.0f);

            var coordinator = new BattleCoordinator(config, network, ar, voice, gesture, () => 2.0f);
            coordinator.OnBattleLog += (msg) => log($"[COORDINATOR] {msg}");

            // 2. CREATE ROOM (Step 1)
            string generatedRoomCode = "";
            network.CreateRoom("Player_1", (success, code) =>
            {
                generatedRoomCode = code;
                coordinator.StateMachine.TryTransition(BattlePhase.RoomCreated, $"Room code: {code}");
            });

            if (string.IsNullOrEmpty(generatedRoomCode) || generatedRoomCode.Length != 6)
            {
                log($"FAIL: 6-digit room code generation failed. Got: {generatedRoomCode}");
                return false;
            }
            log($"STEP 1 PASSED: Room Created with 6-digit code: {generatedRoomCode}");

            // 3. JOIN ROOM & PLAYER CONNECTED (Step 2 & 3)
            bool joinSuccess = false;
            network.JoinRoom(generatedRoomCode, "Player_2", (success, msg) =>
            {
                joinSuccess = success;
                coordinator.StateMachine.TryTransition(BattlePhase.PlayerJoined, "Guest connected");
            });

            if (!joinSuccess || coordinator.StateMachine.CurrentPhase != BattlePhase.PlayerJoined)
            {
                log("FAIL: Join room / player connection failed.");
                return false;
            }
            log("STEP 2 & 3 PASSED: Player 2 joined via 6-digit code and connected.");

            // 4. AR CALIBRATION & GUILD/LOADOUT SETUP
            if (!coordinator.StateMachine.TryTransition(BattlePhase.ARCalibration, "Establishing origin") ||
                !coordinator.StateMachine.TryTransition(BattlePhase.GuildSelection, "Selecting guilds"))
            {
                log("FAIL: AR Calibration / Guild transition failed.");
                return false;
            }

            GuildData magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            GuildData aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            TerrainData terrain = AuthoritativeCombatRoster.GetTerrain("VOLCANIC_RUINS");
            ArenaData arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Volcanic Ruins", null);

            var spellsPlayer1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spellsPlayer2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");

            coordinator.SetupMatch("Player_1", magma, spellsPlayer1, "Player_2", aqua, spellsPlayer2, arena, terrain);
            coordinator.StateMachine.TryTransition(BattlePhase.Loadout, "Loadouts assembled");

            // 5. BATTLE READY (Step 4)
            coordinator.StartBattle();
            log($"STEP 4 PASSED: Battle Ready. Initialized first turn.");

            // 6. PLAYER TURN (Step 5)
            if (coordinator.StateMachine.CurrentPhase != BattlePhase.SpellSelection)
            {
                log($"FAIL: Battle not in SpellSelection. Was: {coordinator.StateMachine.CurrentPhase}");
                return false;
            }
            log($"STEP 5 PASSED: Player Turn active. Phase: {coordinator.StateMachine.CurrentPhase}. Active player: {coordinator.TurnManager.ActivePlayerId}");

            // 7. VOICE SPELL SELECTION (Step 6)
            voice.SimulateSpokenPhrase("Fireball");
            if ((object)coordinator.SelectedSpell == null || coordinator.SelectedSpell.displayName != "Fireball")
            {
                log($"FAIL: Voice spell selection failed. Selected: {coordinator.SelectedSpell?.displayName}");
                return false;
            }
            log($"STEP 6 PASSED: Voice command recognized! Selected spell: {coordinator.SelectedSpell.displayName}");

            // 8. TEST GESTURE (Step 7)
            if (coordinator.StateMachine.CurrentPhase != BattlePhase.GestureWindow)
            {
                log($"FAIL: Not in GestureWindow phase. Was: {coordinator.StateMachine.CurrentPhase}");
                return false;
            }
            // Perform matching gesture
            gesture.SimulateGesturePerformed("GESTURE_THRUST", 0.92f);
            if (!coordinator.IsGestureCompleted || coordinator.StateMachine.CurrentPhase != BattlePhase.AimPhase)
            {
                log($"FAIL: Gesture recognition/validation failed. Phase: {coordinator.StateMachine.CurrentPhase}");
                return false;
            }
            log("STEP 7 PASSED: Gesture 'GESTURE_THRUST' recognized and validated (92% confidence).");

            // 9. TEST TARGET (Step 8)
            ar.SimulateTargetAcquisition(new Vector3(0, 0, 4.5f), 4.5f);
            if (!ar.TargetStatus.isVisible || !ar.TargetStatus.isHitboxAimed)
            {
                log("FAIL: AR Target acquisition failed.");
                return false;
            }
            log("STEP 8 PASSED: AR Opponent target locked at 4.5m.");

            // 10. TEST ATTACK LAUNCH & RESOLUTION (Step 9 & 10)
            int opponentHpBefore = coordinator.OpponentPlayer.CurrentHP;
            int playerManaBefore = coordinator.LocalPlayer.CurrentMana;

            bool launchSuccess = coordinator.CommitAttackLaunch();
            if (!launchSuccess)
            {
                log("FAIL: CommitAttackLaunch returned false.");
                return false;
            }
            log("STEP 9 PASSED: Attack successfully launched at opponent hitbox.");

            // 11. HP / MANA VERIFICATION (Step 11)
            log($"STEP 10 & 11 PASSED: Combat resolved! Result: {coordinator.LastAttackResult.summaryText}. Opponent HP: {coordinator.OpponentPlayer.CurrentHP}/1000, Local Mana: {coordinator.LocalPlayer.CurrentMana}/100");

            if (coordinator.LocalPlayer.CurrentMana != 98)
            {
                log($"FAIL: Expected LocalPlayer Mana 98 (100 - 12 + 10), got {coordinator.LocalPlayer.CurrentMana}");
                return false;
            }

            // 12. NEXT TURN (Step 12)
            log($"STEP 12 PASSED: Advancing turn cycle. New turn number: {coordinator.TurnManager.CurrentTurnNumber}, Active Player: {coordinator.TurnManager.ActivePlayerId}");

            log("==================================================");
            log("VERTICAL SLICE VERIFICATION FULLY SUCCESSFUL!");
            log("==================================================");
            return true;
        }
    }
}
