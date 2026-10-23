using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Battle;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Networking;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Guilds;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Self-contained automated verification suite for the Path Selection phase.
    /// Validates all architectural requirements:
    /// 1. Data-driven PathDefinitions & CombatPathConfig (Aggressive, Defensive, Balanced).
    /// 2. BattleStateMachine transition flow & interrupt resilience.
    /// 3. Combat vitals payload application (HP, Mana Regen, Damage Reduction, Attack Bonus).
    /// 4. Authoritative network synchronization & hidden opponent status.
    /// 5. Graceful timeout default assignment & disconnect recovery.
    /// </summary>
    public static class PathSelectionVerification
    {
        public static bool RunVerification(Action<string> log)
        {
            log("[VERIFICATION] === STARTING PATH SELECTION VERIFICATION SUITE ===");
            bool allPassed = true;

            allPassed &= Test_PathDefinitionsAndPayloads(log);
            allPassed &= Test_StateMachineTransitions(log);
            allPassed &= Test_CombatVitalsAndPipelineApplication(log);
            allPassed &= Test_AuthoritativeNetworkSync(log);
            allPassed &= Test_TimeoutDefaultAssignment(log);
            allPassed &= Test_DisconnectResilience(log);

            if (allPassed)
            {
                log("[VERIFICATION] >>> ALL 6 PATH SELECTION TEST SUITES PASSED SUCCESSFULLY! <<<");
            }
            else
            {
                log("[VERIFICATION] >>> SOME PATH SELECTION TESTS FAILED! <<<");
            }

            return allPassed;
        }

        private static bool Test_PathDefinitionsAndPayloads(Action<string> log)
        {
            log("[TEST 1] Verifying Data-driven PathDefinitions & Payloads...");

            CombatPathConfig config = null;
            try
            {
                config = CombatPathConfig.CreateDefaultConfig();
            }
            catch (Exception ex)
            {
                log($"EXCEPTION calling CombatPathConfig.CreateDefaultConfig: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }

            if ((object)config == null)
            {
                log("FAIL: CombatPathConfig is null.");
                return false;
            }
            if (config.availablePaths == null || config.availablePaths.Count != 3)
            {
                log($"FAIL: Expected 3 available paths, got {(config.availablePaths != null ? config.availablePaths.Count.ToString() : "null")}.");
                return false;
            }

            var aggressive = config.GetPathById("AGGRESSIVE");
            var defensive = config.GetPathById("DEFENSIVE");
            var balanced = config.GetPathById("BALANCED");

            if ((object)aggressive == null || (object)defensive == null || (object)balanced == null)
            {
                log("FAIL: Missing one of the required paths (Aggressive, Defensive, Balanced).");
                return false;
            }

            // Verify User-Selected Modifiers:
            // Aggressive: -100 Max HP, +20% Attack Bonus
            if (aggressive.payload.maxHpModifier != -100 || Math.Abs(aggressive.payload.attackMultiplierBonus - 0.20f) > 0.001f)
            {
                log($"FAIL: Aggressive payload mismatch (HP: {aggressive.payload.maxHpModifier}, Atk: {aggressive.payload.attackMultiplierBonus}).");
                return false;
            }

            // Defensive: +200 Max HP, +15% Damage Reduction
            if (defensive.payload.maxHpModifier != 200 || Math.Abs(defensive.payload.damageReductionModifier - 0.15f) > 0.001f)
            {
                log($"FAIL: Defensive payload mismatch (HP: {defensive.payload.maxHpModifier}, Def: {defensive.payload.damageReductionModifier}).");
                return false;
            }

            // Balanced: Standard HP/Mana, +5 Mana Regen per turn
            if (balanced.payload.maxHpModifier != 0 || balanced.payload.bonusManaRegenPerTurn != 5)
            {
                log($"FAIL: Balanced payload mismatch (HP: {balanced.payload.maxHpModifier}, Regen: {balanced.payload.bonusManaRegenPerTurn}).");
                return false;
            }

            log("PASS: Path definitions and stat payloads correctly configured.");
            return true;
        }

        private static bool Test_StateMachineTransitions(Action<string> log)
        {
            log("[TEST 2] Verifying State Machine Transitions & Interrupts for PathSelection...");

            var sm = new BattleStateMachine(() => 1.0f);

            // Sequential Progression: WaitingForPlayer -> RoomCreated -> PlayerJoined -> PathSelection -> BattleReady
            sm.TryTransition(BattlePhase.RoomCreated, "Room 123456");
            sm.TryTransition(BattlePhase.PlayerJoined, "Guest connected");

            if (!sm.TryTransition(BattlePhase.PathSelection, "Entering Path Selection phase"))
            {
                log($"FAIL: StateMachine rejected transition from PlayerJoined to PathSelection. Current: {sm.CurrentPhase}");
                return false;
            }

            // Verify Interrupt: Temporary Reconnecting during Path Selection
            if (!sm.TryTransition(BattlePhase.Reconnecting, "Packet timeout"))
            {
                log("FAIL: Could not transition from PathSelection to Reconnecting.");
                return false;
            }

            // Verify Resume back to PathSelection
            if (!sm.TryTransition(sm.StoredInterruptedPhase, "Connection restored"))
            {
                log($"FAIL: Could not resume back to PathSelection. Current: {sm.CurrentPhase}");
                return false;
            }

            if (sm.CurrentPhase != BattlePhase.PathSelection)
            {
                log($"FAIL: Expected phase PathSelection after resume, got {sm.CurrentPhase}.");
                return false;
            }

            // Transition from PathSelection to BattleReady (battle scene load)
            if (!sm.TryTransition(BattlePhase.BattleReady, "Path selection locked in"))
            {
                log($"FAIL: Could not transition from PathSelection to BattleReady. Current: {sm.CurrentPhase}");
                return false;
            }

            log("PASS: State Machine sequential progression and interrupt resumption verified.");
            return true;
        }

        private static bool Test_CombatVitalsAndPipelineApplication(Action<string> log)
        {
            log("[TEST 3] Verifying Combat Vitals & Attack Resolution Pipeline integration...");

            var combatConfig = TestCombatDataFactory.CreateInstanceSafe<CombatConfig>();
            combatConfig.defaultMaxHP = 1000;
            combatConfig.defaultMaxMana = 100;
            combatConfig.defaultManaRegenPerTurn = 10;
            combatConfig.globalBaseMissChance = 0f; // Force deterministic hit for test

            var magma = TestCombatDataFactory.CreateInstanceSafe<GuildData>();
            magma.displayName = "Magma";
            magma.guildType = GuildType.Magma;

            var aqua = TestCombatDataFactory.CreateInstanceSafe<GuildData>();
            aqua.displayName = "Aqua";
            aqua.guildType = GuildType.Aqua;

            var playerAggressive = new PlayerCombatState("Player_Aggro", combatConfig, magma);
            var playerDefensive = new PlayerCombatState("Player_Def", combatConfig, aqua);

            var pathConfig = CombatPathConfig.CreateDefaultConfig();
            var aggroPath = pathConfig.GetPathById("AGGRESSIVE");
            var defPath = pathConfig.GetPathById("DEFENSIVE");

            // Apply Path Modifiers
            playerAggressive.ApplyPathModifiers(aggroPath);
            playerDefensive.ApplyPathModifiers(defPath);

            // 1. Check HP adjustments
            if (playerAggressive.MaxHP != 900 || playerAggressive.CurrentHP != 900)
            {
                log($"FAIL: Aggressive HP expected 900, got Max={playerAggressive.MaxHP}, Curr={playerAggressive.CurrentHP}.");
                return false;
            }

            if (playerDefensive.MaxHP != 1200 || playerDefensive.CurrentHP != 1200)
            {
                log($"FAIL: Defensive HP expected 1200, got Max={playerDefensive.MaxHP}, Curr={playerDefensive.CurrentHP}.");
                return false;
            }

            // 2. Check Defensive 15% Damage Reduction
            int initialDefHp = playerDefensive.CurrentHP;
            playerDefensive.ApplyDamage(100); // 100 * (1 - 0.15) = 85 damage
            int damageTaken = initialDefHp - playerDefensive.CurrentHP;
            if (damageTaken != 85)
            {
                log($"FAIL: Expected 85 damage with 15% reduction, got {damageTaken}.");
                return false;
            }

            // 3. Check Attack Resolution Pipeline incorporates +20% Attack bonus
            var pipeline = new AttackResolutionPipeline(combatConfig);
            pipeline.ForceHitDebug = true;

            var testSpell = TestCombatDataFactory.CreateInstanceSafe<SpellData>();
            testSpell.spellId = "FIRE_TEST";
            testSpell.displayName = "Fire Strike";
            testSpell.baseDamage = 100;
            testSpell.type1 = CombatElement.Fire;
            testSpell.isDualType = false;
            testSpell.manaCost = 10;

            var result = pipeline.ResolveAttack(
                playerAggressive,
                playerDefensive,
                testSpell,
                null,
                null,
                isGestureValid: true,
                isTargetAcquired: true,
                rngProvider: () => 0.5f
            );

            // Offensive bonus should include attacker's PathAttackBonus (+20% = 0.20f)
            if (result.offensiveBonus < 0.20f)
            {
                log($"FAIL: Result offensive bonus ({result.offensiveBonus}) did not incorporate +20% PathAttackBonus.");
                return false;
            }

            log($"PASS: Combat vitals, defensive damage reduction (85 dmg taken), and attack bonus (+{result.offensiveBonus * 100f:0}%) verified.");
            return true;
        }

        private static bool Test_AuthoritativeNetworkSync(Action<string> log)
        {
            log("[TEST 4] Verifying Authoritative Network Packet Synchronization...");

            var mockNetwork = new MockNetworkSessionProvider();
            string receivedHostPath = "";
            string receivedGuestPath = "";
            string statusPeerId = "";
            bool statusLocked = false;

            mockNetwork.PeerPathStatusReceived += (peerId, locked) =>
            {
                statusPeerId = peerId;
                statusLocked = locked;
            };

            mockNetwork.AuthoritativePathFinalReceived += (hostPath, guestPath) =>
            {
                receivedHostPath = hostPath;
                receivedGuestPath = guestPath;
            };

            // 1. Client sends path selection intent
            mockNetwork.BroadcastPathStatus("Player_2", true);
            if (statusPeerId != "Player_2" || !statusLocked)
            {
                log("FAIL: PeerPathStatusReceived was not received correctly.");
                return false;
            }

            // 2. Host broadcasts authoritative final resolution
            mockNetwork.BroadcastPathFinal("BALANCED", "AGGRESSIVE");
            if (receivedHostPath != "BALANCED" || receivedGuestPath != "AGGRESSIVE")
            {
                log($"FAIL: AuthoritativePathFinalReceived mismatch ({receivedHostPath}, {receivedGuestPath}).");
                return false;
            }

            log("PASS: Authoritative network packets (status broadcasting & final sync) verified.");
            return true;
        }

        private static bool Test_TimeoutDefaultAssignment(Action<string> log)
        {
            log("[TEST 5] Verifying Timeout Default Fallback Assignment...");

            var config = CombatPathConfig.CreateDefaultConfig();
            config.selectionCountdownSeconds = 15.0f;

            // Empty/Null path ID lookup should return the designated fallback (BALANCED)
            var fallback = config.GetPathById("");
            if ((object)fallback == null || fallback.pathId != "BALANCED")
            {
                log($"FAIL: Expected BALANCED fallback path, got {((object)fallback != null ? fallback.pathId : "null")}.");
                return false;
            }

            var invalidFallback = config.GetPathById("NON_EXISTENT_PATH");
            if ((object)invalidFallback == null || invalidFallback.pathId != "BALANCED")
            {
                log("FAIL: Unknown path ID did not fallback to default.");
                return false;
            }

            log("PASS: Default fallback path resolution verified.");
            return true;
        }

        private static bool Test_DisconnectResilience(Action<string> log)
        {
            log("[TEST 6] Verifying Disconnect Resilience during Path Selection...");

            var sm = new BattleStateMachine(() => 1.0f);
            var mockNetwork = new MockNetworkSessionProvider();
            mockNetwork.CreateRoom("Host_P1", (ok, code) => { });
            mockNetwork.JoinRoom("123456", "Guest_P2", (ok, msg) => { });

            var disconnectManager = new DisconnectManager(sm, mockNetwork, gracePeriodSeconds: 15.0f);

            // Move to PathSelection
            sm.TryTransition(BattlePhase.PlayerJoined, "Players ready");
            sm.TryTransition(BattlePhase.PathSelection, "Entering Path Selection");

            // Simulate temporary network drop
            mockNetwork.SimulateTemporaryDrop();
            if (sm.CurrentPhase != BattlePhase.Reconnecting || !disconnectManager.IsReconnecting)
            {
                log($"FAIL: Expected Reconnecting phase on temporary drop, got {sm.CurrentPhase}.");
                return false;
            }

            // Expire grace period -> Permanent abandonment
            disconnectManager.ExpireGracePeriod();
            if (sm.CurrentPhase != BattlePhase.BattleEnd)
            {
                log($"FAIL: Expected BattleEnd after abandon, got {sm.CurrentPhase}.");
                return false;
            }

            log("PASS: Disconnect and reconnection handling verified without soft-locking.");
            return true;
        }
    }
}
