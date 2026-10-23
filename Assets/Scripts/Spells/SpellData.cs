using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArGuildEncounter.Spells
{
    /// <summary>
    /// Data-driven ScriptableObject definition for spells per COMBAT-BALANCE-V1.
    /// Strictly separates damageClass (L/M/H/SH) from castTier (Tier1/Tier2/Tier3).
    /// </summary>
    [CreateAssetMenu(fileName = "Spell_New", menuName = "AR Guild Encounter/Spell Data")]
    public class SpellData : ScriptableObject
    {
        [Header("Identity")]
        public string spellId = "TEST_SPELL_ID";
        public string displayName = "TEST_SPELL_NAME";
        [TextArea(2, 4)]
        public string description = "Authoritative combat spell per COMBAT-BALANCE-V1.";

        [Header("Classification: Damage Class & Cast Tier (Separated)")]
        [Tooltip("Damage Class determines base damage: L (60), M (100), H (140), SH (180), Fallback (40).")]
        public DamageClass damageClass = DamageClass.L;
        [Tooltip("Cast Tier determines authoritative cast timer window: Tier 1 (5s), Tier 2 (4s), Tier 3 (3s).")]
        public SpellTier castTier = SpellTier.Tier1;

        // Legacy convenience accessor
        public SpellTier tier
        {
            get => castTier;
            set => castTier = value;
        }

        [Header("Elemental Affiliation (Single or Dual-Type)")]
        public CombatElement type1 = CombatElement.Null;
        public bool isDualType = false;
        public CombatElement type2 = CombatElement.Null;

        // Legacy convenience accessor
        public CombatElement element
        {
            get => type1;
            set => type1 = value;
        }

        [Header("Voice Summoning")]
        [Tooltip("Phrases and aliases accepted by the speech recognition system to summon this spell.")]
        public List<string> voiceAliases = new List<string>();

        [Header("Timing & Gesture")]
        [Tooltip("Authoritative duration allowed for somatic gesture casting (Tier 1: 5s, Tier 2: 4s, Tier 3: 3s).")]
        public float castWindow = 5.0f;
        public float castDurationSeconds { get => castWindow; set => castWindow = value; }
        [Tooltip("Identifier used by gesture recognition pipeline to validate physical hand shape.")]
        public string gestureId = "GESTURE_THRUST";

        [Header("Combat Stats")]
        [Tooltip("Mana required to cast. Deducted when attack is committed.")]
        public int manaCost = 12;
        [Tooltip("Base damage value before modifiers per COMBAT-BALANCE-V1 (L=60, M=100, H=140, SH=180, Fallback=40).")]
        public int baseDamage = 60;
        [Tooltip("Optional additive miss modifier for this spell. Added to global 7.5% base miss chance.")]
        public float missModifier = 0.0f;

        [Header("Special Combat Effects")]
        [Tooltip("True if this move heals 40% of final damage actually dealt.")]
        public bool healsCaster = false;
        [Tooltip("True if this move deals self-damage (default 100 HP = 10% of Max HP).")]
        public bool dealsSelfDamage = false;
        public int selfDamageAmount = 100;
        [Tooltip("Applies Move Skip to opponent: skips their next attack and grants 1-action Control Immunity.")]
        public bool isMoveSkip = false;
        [Tooltip("Applies Force Fallback to opponent: forces their next attack to Slot 6 (Struggle/Bluff) and grants 1-action Control Immunity.")]
        public bool isForceFallback = false;

        [Header("Status Effect Integration")]
        [Tooltip("ID of status effect applied on hit, or empty if none.")]
        public string statusEffectId = "";
        [Tooltip("Probability of status infliction (0.0 to 1.0).")]
        public float statusChance = 1.0f;

        [Header("Special Rules")]
        public bool isCriticalEligible = true;
        [Tooltip("Whether this attack can be randomly chosen by Gambit's Slot 6 Bluff ability.")]
        public bool isBluffEligible = true;

        public string spellName { get => displayName; set => displayName = value; }
        public bool bluffEligible { get => isBluffEligible; set => isBluffEligible = value; }
        public bool criticalEligible { get => isCriticalEligible; set => isCriticalEligible = value; }
        public float baseMissChance { get => missModifier; set => missModifier = value; }

        [Header("Presentation (Placeholders)")]
        public string vfxPrefabName = "VFX_Placeholder";
        public string sfxClipName = "SFX_Placeholder";
        public Sprite spellIcon;

        /// <summary>
        /// Checks whether the spell contains the specified elemental type (in type1 or type2).
        /// </summary>
        public bool HasType(CombatElement targetType)
        {
            if (type1 == targetType) return true;
            if (isDualType && type2 == targetType) return true;
            return false;
        }

        /// <summary>
        /// Validates whether a spoken phrase matches any configured voice aliases.
        /// Performs case-insensitive trimmed matching.
        /// </summary>
        public bool MatchesVoiceAlias(string spokenPhrase)
        {
            if (string.IsNullOrWhiteSpace(spokenPhrase)) return false;
            string cleanSpoken = spokenPhrase.Trim();

            // Match display name
            if (cleanSpoken.Equals(displayName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Match configured aliases
            if (voiceAliases != null)
            {
                for (int i = 0; i < voiceAliases.Count; i++)
                {
                    if (cleanSpoken.Equals(voiceAliases[i]?.Trim(), StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}

