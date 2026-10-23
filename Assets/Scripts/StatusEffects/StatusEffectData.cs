using System;
using UnityEngine;

namespace ArGuildEncounter.StatusEffects
{
    public enum StatusStackingRule
    {
        None = 0,
        RefreshDuration = 1,
        StackIntensity = 2,
        StackDuration = 3
    }

    public enum StatusRemovalCondition
    {
        DurationExpiry = 0,
        TurnEnd = 1,
        OnTakeDamage = 2,
        SpecialAbility = 3
    }

    /// <summary>
    /// Generic status-effect framework data asset.
    /// Supports Damage-Over-Time, combat stat debuffs, stacking rules, and duration.
    /// All numerical values remain configurable placeholders.
    /// </summary>
    [CreateAssetMenu(fileName = "Status_New", menuName = "AR Guild Encounter/Status Effect Data")]
    public class StatusEffectData : ScriptableObject
    {
        [Header("Identity")]
        public string statusId = "TEST_STATUS";
        public string displayName = "Test Status";
        [TextArea(2, 3)]
        public string description = "Placeholder status effect description.";

        [Header("Duration & Stacking")]
        [Tooltip("Number of turns or rounds this status persists.")]
        public int durationTurns = 3;
        public StatusStackingRule stackingRule = StatusStackingRule.RefreshDuration;
        public int maxStacks = 1;
        public StatusRemovalCondition removalCondition = StatusRemovalCondition.DurationExpiry;

        [Header("Configurable Modifiers (Placeholders)")]
        [Tooltip("Damage dealt to affected player at turn start per stack.")]
        public int damagePerTurn = 0;
        [Tooltip("Additive miss chance modifier imposed on affected player's attacks.")]
        public float missModifier = 0.0f;
        [Tooltip("Damage dealt multiplier modifier (1.0 = baseline).")]
        public float outgoingDamageMultiplier = 1.0f;
        [Tooltip("Damage taken multiplier modifier (1.0 = baseline).")]
        public float incomingDamageMultiplier = 1.0f;

        [Header("Presentation (Placeholders)")]
        public Sprite icon;
        public string visualEffectPrefabName = "VFX_Status_Placeholder";
    }
}
