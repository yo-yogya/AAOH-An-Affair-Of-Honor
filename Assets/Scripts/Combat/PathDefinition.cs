using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.Combat
{
    public enum PathArchetype
    {
        Aggressive = 0,
        Defensive = 1,
        Balanced = 2
    }

    [Serializable]
    public struct PathStatPayload
    {
        [Tooltip("Direct Max HP modifier (e.g. -100 for Aggressive, +200 for Defensive).")]
        public int maxHpModifier;

        [Tooltip("Direct Max Mana modifier.")]
        public int maxManaModifier;

        [Tooltip("Additional offensive bonus multiplier (e.g. +0.20f for Aggressive).")]
        public float attackMultiplierBonus;

        [Tooltip("Damage reduction percentage against incoming attacks (e.g. 0.15f for Defensive).")]
        public float damageReductionModifier;

        [Tooltip("Bonus mana regenerated each turn (e.g. +5 for Balanced).")]
        public int bonusManaRegenPerTurn;

        [Tooltip("World spawn offset applied to player starting position.")]
        public Vector3 spawnOffset;
    }

    /// <summary>
    /// Configurable data payload for a combat path chosen during the Path Selection phase.
    /// Ingested by PlayerCombatState and PlayerEntity before battle starts.
    /// </summary>
    [CreateAssetMenu(fileName = "Path_New", menuName = "AR Guild Encounter/Path Definition")]
    public class PathDefinition : ScriptableObject
    {
        [Header("Identity")]
        public PathArchetype archetype = PathArchetype.Balanced;
        public string pathId = "BALANCED";
        public string displayName = "Balanced";
        [TextArea(2, 4)]
        public string description = "A versatile path maintaining equilibrium between assault and endurance.";
        public Color themeColor = new Color(0.2f, 0.7f, 0.9f);

        [Header("Data Payload")]
        public PathStatPayload payload;

        [Header("Loadout Modifiers (Optional)")]
        [Tooltip("Optional bonus or override starting spells.")]
        public List<SpellData> startingSpellsOverride = new List<SpellData>();

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

        public static PathDefinition CreateInstance(
            string pathId,
            string displayName,
            string description,
            PathArchetype archetype,
            int hpMod,
            int manaMod,
            float attackBonus,
            float dmgReduction,
            int manaRegenBonus,
            Vector3 spawnOffset,
            Color themeColor)
        {
            var path = CreateSafe<PathDefinition>();
            if ((object)path == null) path = new PathDefinition();
            path.pathId = pathId;
            path.displayName = displayName;
            path.description = description;
            path.archetype = archetype;
            path.themeColor = themeColor;
            if (path.startingSpellsOverride == null) path.startingSpellsOverride = new List<SpellData>();
            path.payload = new PathStatPayload
            {
                maxHpModifier = hpMod,
                maxManaModifier = manaMod,
                attackMultiplierBonus = attackBonus,
                damageReductionModifier = dmgReduction,
                bonusManaRegenPerTurn = manaRegenBonus,
                spawnOffset = spawnOffset
            };
            return path;
        }
    }
}
