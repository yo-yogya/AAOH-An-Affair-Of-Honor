using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Environment;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Verification harness for Phase D: HP, Mana, Turn, Accuracy, and Attack Resolution.
    /// </summary>
    public static class CombatFrameworkVerification
    {
        public static bool RunVerification(Action<string> log)
        {
            log("--- STARTING COMBAT FRAMEWORK VERIFICATION ---");

            CombatConfig config = TestCombatDataFactory.CreateTestCombatConfig();
            GuildData magmaGuild = TestCombatDataFactory.CreateTestGuild(GuildType.Magma, "Magma");
            GuildData gambitGuild = TestCombatDataFactory.CreateTestGuild(GuildType.Gambit, "Gambit", usesBluff: true);
            EnvironmentData env = TestCombatDataFactory.CreateTestEnvironment(EnvironmentPreset.Sunlight, "Sunlight");

            var playerA = new PlayerCombatState("Player_A", config, magmaGuild);
            var playerB = new PlayerCombatState("Player_B", config, gambitGuild);

            // Test 1: Initial HP & Mana
            if (playerA.CurrentHP != config.defaultMaxHP || playerA.CurrentMana != config.defaultMaxMana)
            {
                log($"FAIL: Initial stats invalid. HP={playerA.CurrentHP}, Mana={playerA.CurrentMana}");
                return false;
            }

            // Test 2: Mana Deduction & Clamping
            playerA.TryDeductMana(30);
            if (playerA.CurrentMana != config.defaultMaxMana - 30)
            {
                log($"FAIL: Mana deduction failed. CurrentMana={playerA.CurrentMana}");
                return false;
            }

            // Test 3: Damage & HP Clamping
            playerA.ApplyDamage(40);
            if (playerA.CurrentHP != config.defaultMaxHP - 40)
            {
                log($"FAIL: HP damage failed. CurrentHP={playerA.CurrentHP}");
                return false;
            }

            // Test 4: Turn Manager
            var turnManager = new TurnManager(config, "Player_A", "Player_B", playerA, playerB);
            turnManager.InitializeFirstTurn(() => 1.0f); // Force Player_A first
            if (turnManager.ActivePlayerId != "Player_A")
            {
                log($"FAIL: First player not Player_A. Was: {turnManager.ActivePlayerId}");
                return false;
            }

            turnManager.AdvanceTurn();
            if (turnManager.ActivePlayerId != "Player_B")
            {
                log($"FAIL: Turn alternation failed. Active={turnManager.ActivePlayerId}");
                return false;
            }

            // Test 5: Additive Miss Chance (7.5% Base Miss + Modifiers)
            var pipeline = new AttackResolutionPipeline(config);
            var testSpell = TestCombatDataFactory.CreateTestSpell("TEST_FIRE", "Fireball", CombatElement.Fire, SpellTier.Tier1, 10, 30);
            testSpell.missModifier = 0.025f; // +2.5%

            // Total miss should be 7.5% + 2.5% = 10.0%
            var missResult = pipeline.ResolveAttack(playerA, playerB, testSpell, env, null, true, true, () => 0.05f); // roll 5% < 10% miss
            if (missResult.isHit)
            {
                log($"FAIL: Attack should have missed with roll 0.05 vs 0.10 miss chance.");
                return false;
            }
            if (Mathf.Abs(missResult.finalMissChance - 0.10f) > 0.001f)
            {
                log($"FAIL: Final miss chance incorrect: {missResult.finalMissChance} (Expected: 0.10)");
                return false;
            }

            // Test 6: Hit resolution
            var hitResult = pipeline.ResolveAttack(playerA, playerB, testSpell, env, null, true, true, () => 0.5f);
            if (!hitResult.isHit || hitResult.finalDamageDealt <= 0)
            {
                log($"FAIL: Attack should have hit with roll 0.50. Hit={hitResult.isHit}, Dmg={hitResult.finalDamageDealt}");
                return false;
            }

            // Test 7: Gambit Bluff 75% damage and self-damage
            var bluffSpell = TestCombatDataFactory.CreateInstanceSafe<SpellData>();
            bluffSpell.spellId = "BLUFF";
            bluffSpell.displayName = "Bluff";
            bluffSpell.manaCost = 0;
            bluffSpell.baseDamage = 0;

            playerB.SetLoadout(new List<SpellData> { testSpell }, bluffSpell);

            // Roll 0.05 triggers backfire (< 0.08)
            int hpBefore = playerB.CurrentHP;
            var bluffResult = pipeline.ResolveAttack(playerB, playerA, bluffSpell, env, null, true, true, () => 0.05f);
            if (bluffResult.rawBaseDamage != Mathf.RoundToInt(testSpell.baseDamage * 0.75f))
            {
                log($"FAIL: Bluff rawBaseDamage was {bluffResult.rawBaseDamage}, expected {Mathf.RoundToInt(testSpell.baseDamage * 0.75f)}");
                return false;
            }
            if (!bluffResult.didSelfDamageTrigger)
            {
                log("FAIL: Bluff did not trigger self-damage on roll 0.10 < 0.15");
                return false;
            }
            if (playerB.CurrentHP >= hpBefore)
            {
                log("FAIL: Player B did not take self damage from Bluff backlash");
                return false;
            }

            log("--- ALL COMBAT FRAMEWORK TESTS PASSED ---");
            return true;
        }
    }
}
