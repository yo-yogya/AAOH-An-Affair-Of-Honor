using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.Combat
{
    public enum FirstPlayerRule
    {
        Random = 0,
        HostFirst = 1,
        ClientFirst = 2
    }

    [Serializable]
    public struct ElementEffectivenessEntry
    {
        public CombatElement attackingElement;
        public CombatElement defendingElement;
        public float multiplier;

        public ElementEffectivenessEntry(CombatElement attack, CombatElement defense, float mult)
        {
            attackingElement = attack;
            defendingElement = defense;
            multiplier = mult;
        }
    }

    /// <summary>
    /// Centralized combat configuration asset.
    /// Ingests all numerical rules from COMBAT-BALANCE-V1 without requiring code rewrites.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatConfig_v001", menuName = "AR Guild Encounter/Combat Config")]
    public class CombatConfig : ScriptableObject
    {
        [Header("Configuration Versioning")]
        public string configVersion = "COMBAT-BALANCE-V1";

        [Header("Authoritative Resources (COMBAT-BALANCE-V1)")]
        public int defaultMaxHP = 1000;
        public int defaultStartingHP = 1000;
        public int defaultMaxMana = 100;
        public int defaultStartingMana = 100;
        public int defaultManaRegenPerTurn = 10;
        [Tooltip("Portion of mana cost deducted on failed gesture or timeout cast.")]
        public float failedCastManaCostMultiplier = 1.0f;

        [Header("Global Accuracy & Miss Chance")]
        [Tooltip("Global base miss chance is strictly 7.5% per specification.")]
        public float globalBaseMissChance = 0.075f;
        [Tooltip("Maximum allowed final miss chance (35%).")]
        public float maxFinalMissChance = 0.35f;

        [Header("Authoritative Offensive Modifiers & Cap")]
        [Tooltip("Combined offensive bonus cap from Guild, Terrain, and Buffs is strictly +50%.")]
        public float offensiveBonusCap = 0.50f;
        public float guildOffensiveBonus = 0.25f;
        public float terrainOffensiveBonus = 0.25f;

        [Header("Type Effectiveness Matchup Values")]
        public float superEffectiveMultiplier = 1.25f;
        public float neutralMultiplier = 1.00f;
        public float resistedMultiplier = 0.80f;

        [Header("Authoritative Cast Windows")]
        public float tier1CastWindowSeconds = 5.0f;
        public float tier2CastWindowSeconds = 4.0f;
        public float tier3CastWindowSeconds = 3.0f;

        [Header("Turn System Configuration")]
        public FirstPlayerRule firstPlayerRule = FirstPlayerRule.Random;

        [Header("Gambit & Critical System")]
        [Tooltip("Gambit locked critical chance (45%).")]
        public float gambitCriticalChance = 0.45f;
        [Tooltip("Gambit locked critical damage multiplier (1.25x).")]
        public float gambitCriticalMultiplier = 1.25f;

        [Header("Gambit Bluff Mechanics")]
        [Tooltip("Bluff uses 75% of selected attack base damage.")]
        public float bluffDamageMultiplier = 0.75f;
        [Tooltip("Bluff has an 8% chance to trigger backfire self-damage.")]
        public float bluffSelfDamageChance = 0.08f;
        [Tooltip("When backfire triggers, inflicts 10% of player max HP (100 HP).")]
        public float bluffSelfDamagePercentOfMaxHp = 0.10f;

        [Header("Struggle Mechanics")]
        [Tooltip("Struggle always costs 0 mana.")]
        public readonly int struggleManaCost = 0;
        [Tooltip("Struggle base damage per COMBAT-BALANCE-V1 is strictly 40.")]
        public int struggleBaseDamage = 40;
        [Tooltip("Struggle and Bluff are selectable ONLY when affordable moves == 0 or Force Fallback is active.")]
        public bool struggleAlwaysSelectable = false;

        [Header("Healing & Self-Damage")]
        [Tooltip("Heal moves restore 40% of final damage actually dealt.")]
        public float healingPercentOfFinalDamage = 0.40f;
        [Tooltip("Default self-damage is 10% of max HP (100 HP).")]
        public float defaultSelfDamagePercentOfMaxHp = 0.10f;

        [Header("Control Durations")]
        public int moveSkipDurationActions = 1;
        public int forceFallbackDurationActions = 1;
        public int controlImmunityDurationActions = 1;

        [Header("Elemental Effectiveness Matrix")]
        public List<ElementEffectivenessEntry> typeChart = new List<ElementEffectivenessEntry>();

        public static List<ElementEffectivenessEntry> CreateAuthoritativeTypeChart()
        {
            return new List<ElementEffectivenessEntry>
            {
                // Fire
                new ElementEffectivenessEntry(CombatElement.Fire, CombatElement.Grass, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Fire, CombatElement.Ice, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Fire, CombatElement.Fire, 0.80f),
                new ElementEffectivenessEntry(CombatElement.Fire, CombatElement.Water, 0.80f),

                // Water
                new ElementEffectivenessEntry(CombatElement.Water, CombatElement.Fire, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Water, CombatElement.Ground, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Water, CombatElement.Water, 0.80f),
                new ElementEffectivenessEntry(CombatElement.Water, CombatElement.Grass, 0.80f),

                // Wind
                new ElementEffectivenessEntry(CombatElement.Wind, CombatElement.Grass, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Wind, CombatElement.Lightning, 0.80f),

                // Ice
                new ElementEffectivenessEntry(CombatElement.Ice, CombatElement.Grass, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Ice, CombatElement.Wind, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Ice, CombatElement.Ground, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Ice, CombatElement.Fire, 0.80f),
                new ElementEffectivenessEntry(CombatElement.Ice, CombatElement.Water, 0.80f),
                new ElementEffectivenessEntry(CombatElement.Ice, CombatElement.Ice, 0.80f),

                // Lightning
                new ElementEffectivenessEntry(CombatElement.Lightning, CombatElement.Water, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Lightning, CombatElement.Wind, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Lightning, CombatElement.Lightning, 0.80f),
                new ElementEffectivenessEntry(CombatElement.Lightning, CombatElement.Grass, 0.80f),

                // Ground
                new ElementEffectivenessEntry(CombatElement.Ground, CombatElement.Fire, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Ground, CombatElement.Lightning, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Ground, CombatElement.Grass, 0.80f),

                // Grass
                new ElementEffectivenessEntry(CombatElement.Grass, CombatElement.Water, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Grass, CombatElement.Ground, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Grass, CombatElement.Fire, 0.80f),
                new ElementEffectivenessEntry(CombatElement.Grass, CombatElement.Grass, 0.80f),

                // Light
                new ElementEffectivenessEntry(CombatElement.Light, CombatElement.Dark, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Light, CombatElement.Light, 0.80f),

                // Dark
                new ElementEffectivenessEntry(CombatElement.Dark, CombatElement.Light, 1.25f),
                new ElementEffectivenessEntry(CombatElement.Dark, CombatElement.Dark, 0.80f)
            };
        }

        private static List<ElementEffectivenessEntry> _defaultTypeChart;

        /// <summary>
        /// Retrieves element multiplier for attacker vs defender.
        /// Defaults to 1.0f (neutral) if unlisted.
        /// </summary>
        public float GetElementMultiplier(CombatElement attack, CombatElement defense)
        {
            var chart = (typeChart != null && typeChart.Count > 0) ? typeChart : (_defaultTypeChart ?? (_defaultTypeChart = CreateAuthoritativeTypeChart()));
            for (int i = 0; i < chart.Count; i++)
            {
                if (chart[i].attackingElement == attack && chart[i].defendingElement == defense)
                {
                    return chart[i].multiplier;
                }
            }
            return neutralMultiplier;
        }

        /// <summary>
        /// Calculates single or averaged dual-type effectiveness against defending element.
        /// Per COMBAT-BALANCE-V1 Section 7: Average the two matchups. Never multiply them.
        /// </summary>
        public float GetDualTypeMultiplier(CombatElement type1, CombatElement type2, bool isDualType, CombatElement defense)
        {
            float mult1 = GetElementMultiplier(type1, defense);
            if (!isDualType) return mult1;

            float mult2 = GetElementMultiplier(type2, defense);
            return (mult1 + mult2) * 0.5f;
        }

        public float GetCastWindowForTier(SpellTier tier)
        {
            switch (tier)
            {
                case SpellTier.Tier1: return tier1CastWindowSeconds;
                case SpellTier.Tier2: return tier2CastWindowSeconds;
                case SpellTier.Tier3: return tier3CastWindowSeconds;
                default: return tier1CastWindowSeconds;
            }
        }
    }
}

