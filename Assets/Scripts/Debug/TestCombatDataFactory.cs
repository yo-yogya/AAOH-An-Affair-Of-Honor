using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Environment;
using ArGuildEncounter.StatusEffects;
using ArGuildEncounter.Combat;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Factory that produces test/placeholder combat assets marked strictly with TEST_ prefixes.
    /// Used to prove systems before COMBAT-BALANCE-V1 is ingested.
    /// Safe for both inside Unity Editor and standalone CI/test environments.
    /// </summary>
    public static class TestCombatDataFactory
    {
        public static T CreateInstanceSafe<T>() where T : ScriptableObject, new()
        {
            try
            {
                return ScriptableObject.CreateInstance<T>();
            }
            catch
            {
                try
                {
                    return new T();
                }
                catch
                {
                    return (T)FormatterServices.GetUninitializedObject(typeof(T));
                }
            }
        }

        public static CombatConfig CreateTestCombatConfig()
        {
            var config = CreateInstanceSafe<CombatConfig>();
            config.configVersion = "COMBAT-BALANCE-V1";
            config.globalBaseMissChance = 0.075f; // Locked 7.5%
            config.maxFinalMissChance = 0.35f;   // Locked 35%
            config.offensiveBonusCap = 0.50f;     // Locked +50%
            config.guildOffensiveBonus = 0.25f;
            config.terrainOffensiveBonus = 0.25f;
            config.superEffectiveMultiplier = 1.25f;
            config.neutralMultiplier = 1.0f;
            config.resistedMultiplier = 0.8f;
            config.tier1CastWindowSeconds = 5.0f;
            config.tier2CastWindowSeconds = 4.0f;
            config.tier3CastWindowSeconds = 3.0f;
            config.defaultMaxHP = 1000;
            config.defaultMaxMana = 100;
            config.defaultManaRegenPerTurn = 10;
            config.failedCastManaCostMultiplier = 1.0f;
            config.firstPlayerRule = FirstPlayerRule.Random;
            config.bluffDamageMultiplier = 0.75f;
            config.bluffSelfDamageChance = 0.08f;
            config.bluffSelfDamagePercentOfMaxHp = 0.10f;
            config.gambitCriticalChance = 0.45f;
            config.gambitCriticalMultiplier = 1.25f;
            config.struggleBaseDamage = 40;
            config.struggleAlwaysSelectable = false;
            config.typeChart = CombatConfig.CreateAuthoritativeTypeChart();
            return config;
        }

        public static SpellData CreateTestSpell(
            string id, 
            string name, 
            CombatElement element, 
            SpellTier tier, 
            int manaCost = 15, 
            int baseDamage = 25, 
            string gestureId = "GESTURE_TEST",
            List<string> aliases = null)
        {
            var spell = CreateInstanceSafe<SpellData>();
            spell.spellId = id;
            spell.displayName = name;
            spell.element = element;
            spell.tier = tier;
            spell.manaCost = manaCost;
            spell.baseDamage = baseDamage;
            spell.gestureId = gestureId;
            spell.castWindow = tier == SpellTier.Tier1 ? 5.0f : (tier == SpellTier.Tier2 ? 4.0f : 3.0f);
            spell.missModifier = 0.0f;
            spell.isCriticalEligible = true;
            spell.isBluffEligible = true;
            spell.voiceAliases = aliases ?? new List<string> { name, $"Cast {name}" };
            return spell;
        }

        public static GuildData CreateTestGuild(GuildType type, string name, bool usesBluff = false)
        {
            var guild = CreateInstanceSafe<GuildData>();
            guild.guildType = type;
            guild.displayName = name;
            guild.usesBluffSlot = usesBluff;
            guild.affinityDamageMultiplier = 1.0f; // Baseline TBD
            guild.criticalChanceModifier = (type == GuildType.Gambit) ? 0.45f : 0.0f;
            guild.criticalDamageMultiplier = (type == GuildType.Gambit) ? 1.25f : 1.5f;
            guild.affinityTypes = new List<CombatElement>();
            if (type == GuildType.Magma) { guild.affinityTypes.Add(CombatElement.Fire); guild.affinityTypes.Add(CombatElement.Ground); }
            else if (type == GuildType.Aqua) { guild.affinityTypes.Add(CombatElement.Water); guild.affinityTypes.Add(CombatElement.Ice); }
            else if (type == GuildType.Plasma) { guild.affinityTypes.Add(CombatElement.Lightning); guild.affinityTypes.Add(CombatElement.Wind); }
            else if (type == GuildType.HolyTrinity) { guild.affinityTypes.Add(CombatElement.Light); guild.affinityTypes.Add(CombatElement.Dark); guild.affinityTypes.Add(CombatElement.Null); }
            return guild;
        }

        public static EnvironmentData CreateTestEnvironment(EnvironmentPreset preset, string name)
        {
            var env = CreateInstanceSafe<EnvironmentData>();
            env.preset = preset;
            env.displayName = name;
            env.environmentMissModifier = 0.0f;
            env.damageMultiplier = 1.0f;
            env.criticalChanceModifier = 0.0f;
            env.statusChanceModifier = 0.0f;
            return env;
        }

        public static ArenaData CreateTestArena(ArenaPreset preset, string name, EnvironmentData env)
        {
            var arena = CreateInstanceSafe<ArenaData>();
            arena.arenaPreset = preset;
            arena.arenaName = name;
            arena.defaultEnvironment = env;
            return arena;
        }

        public static StatusEffectData CreateTestStatus(string id, string name, int duration = 2, int damagePerTurn = 5)
        {
            var status = CreateInstanceSafe<StatusEffectData>();
            status.statusId = id;
            status.displayName = name;
            status.durationTurns = duration;
            status.damagePerTurn = damagePerTurn;
            status.stackingRule = StatusStackingRule.RefreshDuration;
            status.maxStacks = 3;
            status.removalCondition = StatusRemovalCondition.DurationExpiry;
            return status;
        }
    }
}
