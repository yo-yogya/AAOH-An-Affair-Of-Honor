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

        /// <summary>
        /// Produces a human-readable high-level summary of the combat action.
        /// </summary>
        public string FormatSummary()
        {
            if (!isHit)
            {
                return $"[Turn {turnNumber}] {attackerId} cast {spellName} -> MISSED (Miss Chance: {finalMissChance:P1}, Roll: {missRoll:F3})";
            }

            string critTag = isCrit ? " [CRITICAL!]" : "";
            string damageInfo = damageDealt > 0 ? $"dealt {damageDealt} DMG ({previousDefenderHp} -> {newDefenderHp} HP)" : "no damage dealt";
            return $"[Turn {turnNumber}] {attackerId} cast {spellName}{critTag} -> {damageInfo}";
        }

        /// <summary>
        /// Generates a comprehensive trace of calculations for authoritative battle diagnostics.
        /// </summary>
        public string GetFullBreakdownTrace()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== COMBAT TRACE [Turn {turnNumber}] ===");
            sb.AppendLine($"Attacker: {attackerId} | Defender: {defenderId} | Spell: {spellName} ({damageClass}, Tier: {castTier})");
            sb.AppendLine($"Hit Roll: {(isHit ? "HIT" : "MISS")} (Roll: {missRoll:F3} vs FinalMissChance: {finalMissChance:P1})");
            if (isHit)
            {
                sb.AppendLine($"Base Dmg: {rawBaseDamage} * TypeMult: {typeMultiplier:F2} * TotalBonus: {totalOffensiveBonus:+0.0%;-0.0%;+0.0%} * Crit: {critMultiplier:F2}");
                sb.AppendLine($"Final Dmg: {finalDamage} (Defender HP: {previousDefenderHp} -> {newDefenderHp})");
                if (healingAmount > 0) sb.AppendLine($"Heal: +{healingAmount} HP (Attacker HP: {previousAttackerHp} -> {newAttackerHp})");
                if (selfDamageAmount > 0) sb.AppendLine($"Self-Damage: -{selfDamageAmount} HP");
                if (backfireAmount > 0) sb.AppendLine($"Backfire: -{backfireAmount} HP");
            }
            sb.AppendLine($"Mana: {previousAttackerMana} - {manaCostDeducted} + {manaRegenAdded} = {finalAttackerMana}");
            if (!string.IsNullOrEmpty(activeModifiers)) sb.AppendLine($"Modifiers: {activeModifiers}");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Converts the diagnostic record into key-value pairs suitable for telemetry or analytics logging.
        /// </summary>
        public Dictionary<string, object> ToTelemetryDictionary()
        {
            return new Dictionary<string, object>
            {
                { "turnNumber", turnNumber },
                { "attackerId", attackerId ?? "" },
                { "defenderId", defenderId ?? "" },
                { "spellName", spellName ?? "" },
                { "damageClass", damageClass ?? "" },
                { "castTier", castTier ?? "" },
                { "isHit", isHit },
                { "isCrit", isCrit },
                { "damageDealt", damageDealt },
                { "healingAmount", healingAmount },
                { "selfDamageAmount", selfDamageAmount },
                { "finalDefenderHp", newDefenderHp },
                { "finalAttackerMana", finalAttackerMana }
            };
        }

        public override string ToString() => FormatSummary();
    }
}
