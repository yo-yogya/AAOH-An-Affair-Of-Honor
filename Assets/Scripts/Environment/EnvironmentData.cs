using System;
using UnityEngine;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.Environment
{
    public enum EnvironmentPreset
    {
        Sunlight = 0,
        Rain = 1,
        Thunderstorm = 2,
        Noon = 3,
        Night = 4,
        Storm = 5
    }

    /// <summary>
    /// Configurable environmental condition affecting combat calculations and presentation.
    /// Numerical modifiers remain TBD placeholders.
    /// </summary>
    [CreateAssetMenu(fileName = "Env_New", menuName = "AR Guild Encounter/Environment Data")]
    public class EnvironmentData : ScriptableObject
    {
        public EnvironmentPreset preset = EnvironmentPreset.Sunlight;
        public string displayName = "TEST_ENVIRONMENT";

        [Header("Combat Modifiers (Placeholders / Configurable)")]
        [Tooltip("Additive miss chance modifier applied to all attacks under this environment.")]
        public float environmentMissModifier = 0.0f;
        [Tooltip("General damage multiplier under this environment (1.0 = baseline).")]
        public float damageMultiplier = 1.0f;
        [Tooltip("Additive critical chance modifier.")]
        public float criticalChanceModifier = 0.0f;
        [Tooltip("Additive status effect infliction chance modifier.")]
        public float statusChanceModifier = 0.0f;

        [Header("Elemental Affinities")]
        [Tooltip("Element that receives boosted effectiveness in this environment.")]
        public CombatElement boostedElement = CombatElement.Null;
        [Tooltip("Damage multiplier for boosted element (1.0 = baseline, TBD).")]
        public float boostedElementMultiplier = 1.0f;

        [Header("Visual / Audio")]
        public string ambientAudioClipName = "";
        public string skyboxOrVfxName = "";
    }
}
