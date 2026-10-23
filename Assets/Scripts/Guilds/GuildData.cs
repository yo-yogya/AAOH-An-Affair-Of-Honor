using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.Guilds
{
    public enum GuildType
    {
        Magma = 0,
        Aqua = 1,
        Plasma = 2,
        HolyTrinity = 3,
        Gambit = 4
    }

    /// <summary>
    /// Configuration data for a Guild affiliation per COMBAT-BALANCE-V1.
    /// Supports passives, elemental affinities, and custom slot 6 fallback behaviors.
    /// </summary>
    [CreateAssetMenu(fileName = "Guild_New", menuName = "AR Guild Encounter/Guild Data")]
    public class GuildData : ScriptableObject
    {
        [Header("Identity")]
        public GuildType guildType = GuildType.Magma;
        public string displayName = "Magma";
        [TextArea(2, 4)]
        public string identityTheme = "Burst / Aggression (Fire + Ground)";

        private string _customGuildId;
        public string guildId
        {
            get => !string.IsNullOrEmpty(_customGuildId) ? _customGuildId : guildType.ToString().ToUpperInvariant();
            set
            {
                _customGuildId = value;
                string sanitized = value?.Replace("_", "");
                if (Enum.TryParse<GuildType>(sanitized, true, out var parsed))
                    guildType = parsed;
            }
        }
        public string guildName { get => displayName; set => displayName = value; }
        public bool isGambitGuild { get => usesBluffSlot || guildType == GuildType.Gambit; set => usesBluffSlot = value; }
        public float gambitCritChance { get => criticalChance; set => criticalChance = value; }
        public float gambitCritMultiplier { get => criticalMultiplier; set => criticalMultiplier = value; }
        public float affinityDamageMultiplier { get => 1.0f + offensiveBonus; set => offensiveBonus = value - 1.0f; }
        public float criticalChanceModifier { get => criticalChance; set => criticalChance = value; }
        public float criticalDamageMultiplier { get => criticalMultiplier; set => criticalMultiplier = value; }

        [Header("Elemental Affinities (COMBAT-BALANCE-V1)")]
        [Tooltip("Affinities that trigger the +25% offensive bonus. Magma: Fire+Ground; Aqua: Water+Ice; Plasma: Lightning+Wind; Holy Trinity: Light+Dark+Null; Gambit: None.")]
        public List<CombatElement> affinityTypes = new List<CombatElement>();
        public float offensiveBonus = 0.25f;

        [Header("Slot 6 Fallback Ability")]
        [Tooltip("True if this guild uses Bluff instead of Struggle (Gambit).")]
        public bool usesBluffSlot = false;

        [Header("Gambit / Critical Modifiers")]
        [Tooltip("Critical chance (Gambit = 0.45f, other guilds = 0.0f baseline).")]
        public float criticalChance = 0.0f;
        [Tooltip("Critical damage multiplier (Gambit = 1.25x, baseline = 1.0x).")]
        public float criticalMultiplier = 1.0f;

        [Header("Presentation (Placeholders)")]
        public Sprite emblemIcon;
        public Color primaryColor = Color.white;

        /// <summary>
        /// Checks if this guild provides an affinity bonus to any type on the spell.
        /// Applied once per attack even if dual-type.
        /// </summary>
        public bool HasAffinityBonus(SpellData spell)
        {
            if (spell is null || affinityTypes is null || affinityTypes.Count == 0) return false;
            for (int i = 0; i < affinityTypes.Count; i++)
            {
                if (spell.HasType(affinityTypes[i])) return true;
            }
            return false;
        }
    }
}

