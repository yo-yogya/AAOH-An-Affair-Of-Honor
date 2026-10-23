using System;
using System.Collections.Generic;

namespace ArGuildEncounter.Combat
{
    /// <summary>
    /// Captures full authoritative combat traces and formula breakdowns
    /// for real-time display in the Combat Diagnostic Panel.
    /// Per COMBAT-BALANCE-V1 Section 8, 16, 17, 28.
    /// </summary>
    [Serializable]
    public class CombatDiagnosticData
    {
        public int turnNumber;
        public string attackerId;
        public string defenderId;
        public string spellName;
        public string damageClass;
        public string castTier;

        // Traces
        public string hpTrace;
        public string manaTrace;
        public string missTrace;
        public string damageTrace;

        // Breakdown Details
        public int previousDefenderHp;
        public int newDefenderHp;
        public int damageDealt;
        public int previousAttackerHp;
        public int newAttackerHp;
        public int healingAmount;
        public int selfDamageAmount;
        public int backfireAmount;

        public int previousAttackerMana;
        public int manaCostDeducted;
        public int afterCostMana;
        public int manaRegenAdded;
        public int finalAttackerMana;

        public float baseMissChance;
        public float spellMissMod;
        public float envMissMod;
        public float statusMissMod;
        public float rawMissChance;
        public float finalMissChance;
        public float missRoll;
        public bool isHit;

        public int rawBaseDamage;
        public float typeMultiplier;
        public float guildBonus;
        public float terrainBonus;
        public float buffBonus;
        public float totalOffensiveBonus;
        public float effectiveOffensiveBonus;
        public float critMultiplier;
        public bool isCrit;
        public int finalDamage;

        public string activeModifiers;
        public string attackerStatuses;
        public string defenderStatuses;
    }
}
