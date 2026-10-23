using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.Combat
{
    [CreateAssetMenu(fileName = "TypeMatchupTable", menuName = "AR Guild Encounter/Type Matchup Table")]
    public class TypeMatchupTable : ScriptableObject
    {
        public const float SuperEffective = 1.25f;
        public const float Neutral = 1.00f;
        public const float Resisted = 0.80f;

        [SerializeField]
        private List<ElementEffectivenessEntry> _entries = new List<ElementEffectivenessEntry>();

        public IReadOnlyList<ElementEffectivenessEntry> Entries => _entries;

        public void PopulateDefaults()
        {
            _entries = CombatConfig.CreateAuthoritativeTypeChart();
        }

        public float GetMultiplier(CombatElement attack, CombatElement defense)
        {
            if (_entries == null || _entries.Count == 0)
            {
                _entries = CombatConfig.CreateAuthoritativeTypeChart();
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].attackingElement == attack && _entries[i].defendingElement == defense)
                {
                    return _entries[i].multiplier;
                }
            }

            return Neutral;
        }

        public float GetDualTypeMultiplier(CombatElement type1, CombatElement type2, bool isDualType, CombatElement defense)
        {
            float m1 = GetMultiplier(type1, defense);
            if (!isDualType) return m1;

            float m2 = GetMultiplier(type2, defense);
            return (m1 + m2) * 0.5f;
        }
    }
}
