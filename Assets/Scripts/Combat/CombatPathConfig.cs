using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArGuildEncounter.Combat
{
    /// <summary>
    /// Authoritative configuration asset for the Path Selection phase.
    /// Holds available paths, countdown duration, and default fallback assignment.
    /// Fully configurable in Unity Inspector or instantiated via code defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatPathConfig", menuName = "AR Guild Encounter/Combat Path Config")]
    public class CombatPathConfig : ScriptableObject
    {
        [Header("Selection Timing")]
        [Tooltip("Authoritative countdown duration in seconds before auto-assigning default path.")]
        public float selectionCountdownSeconds = 15.0f;

        [Header("Available Paths")]
        public List<PathDefinition> availablePaths = new List<PathDefinition>();

        [Header("Default Fallback Path")]
        [Tooltip("Auto-assigned if a player does not select a path before the countdown expires.")]
        public PathDefinition defaultFallbackPath;

        public PathDefinition GetPathById(string pathId)
        {
            if (string.IsNullOrWhiteSpace(pathId)) return defaultFallbackPath;
            if (availablePaths != null)
            {
                for (int i = 0; i < availablePaths.Count; i++)
                {
                    if ((object)availablePaths[i] != null && string.Equals(availablePaths[i].pathId, pathId, StringComparison.OrdinalIgnoreCase))
                    {
                        return availablePaths[i];
                    }
                }
            }
            return defaultFallbackPath;
        }

        private static T CreateSafe<T>() where T : ScriptableObject, new()
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
                    return (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T));
                }
            }
        }

        public static CombatPathConfig CreateDefaultConfig()
        {
            var config = CreateSafe<CombatPathConfig>();
            if ((object)config == null) config = new CombatPathConfig();
            config.selectionCountdownSeconds = 15.0f;
            if (config.availablePaths == null) config.availablePaths = new List<PathDefinition>();

            var aggressive = PathDefinition.CreateInstance(
                "AGGRESSIVE",
                "Aggressive",
                "Unleashes lethal offensive power (+20% Attack Bonus) at the sacrifice of resilience (-100 Max HP).",
                PathArchetype.Aggressive,
                hpMod: -100,
                manaMod: 0,
                attackBonus: 0.20f,
                dmgReduction: 0.0f,
                manaRegenBonus: 0,
                spawnOffset: new Vector3(0f, 0f, 0.35f),
                themeColor: new Color(0.95f, 0.25f, 0.2f)
            );

            var defensive = PathDefinition.CreateInstance(
                "DEFENSIVE",
                "Defensive",
                "Fortified bastion build (+200 Max HP, +15% Damage Reduction) withstanding heavy assaults.",
                PathArchetype.Defensive,
                hpMod: 200,
                manaMod: 0,
                attackBonus: 0.0f,
                dmgReduction: 0.15f,
                manaRegenBonus: 0,
                spawnOffset: new Vector3(0f, 0f, -0.35f),
                themeColor: new Color(0.2f, 0.6f, 0.95f)
            );

            var balanced = PathDefinition.CreateInstance(
                "BALANCED",
                "Balanced",
                "Tactical equilibrium maintaining baseline vitals with enhanced spell energy recovery (+5 Mana Regen/turn).",
                PathArchetype.Balanced,
                hpMod: 0,
                manaMod: 0,
                attackBonus: 0.0f,
                dmgReduction: 0.0f,
                manaRegenBonus: 5,
                spawnOffset: Vector3.zero,
                themeColor: new Color(0.3f, 0.85f, 0.4f)
            );

            config.availablePaths.Add(aggressive);
            config.availablePaths.Add(defensive);
            config.availablePaths.Add(balanced);
            config.defaultFallbackPath = balanced;

            return config;
        }
    }
}
