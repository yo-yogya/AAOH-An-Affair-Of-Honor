using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArGuildEncounter.Spells
{
    /// <summary>
    /// Data-driven ScriptableObject database of spells.
    /// Acts as the production source of truth for spell statistics and configuration.
    /// Designers can add, remove, or modify spell assets (damage, mana cost, type, tier,
    /// effect, gesture, status, Bluff eligibility) in the Inspector or runtime without
    /// modifying the combat engine source code.
    /// </summary>
    [CreateAssetMenu(fileName = "SpellDatabase", menuName = "AR Guild Encounter/Spell Database")]
    public class SpellDatabaseSO : ScriptableObject, ISpellDataProvider
    {
        [Tooltip("The complete collection of configured spell assets for the game.")]
        public List<SpellData> spells = new List<SpellData>();

        private Dictionary<string, SpellData> _lookup;

        private void OnEnable()
        {
            BuildLookup();
        }

        public void BuildLookup()
        {
            _lookup = new Dictionary<string, SpellData>(StringComparer.OrdinalIgnoreCase);
            if (spells == null) return;

            for (int i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];
                if (spell is not null && !string.IsNullOrEmpty(spell.spellId))
                {
                    _lookup[spell.spellId] = spell;
                }
            }
        }

        public SpellData GetSpell(string spellId)
        {
            if (string.IsNullOrEmpty(spellId)) return null;
            if (_lookup == null || _lookup.Count != (spells?.Count ?? 0))
            {
                BuildLookup();
            }

            if (_lookup.TryGetValue(spellId, out var found))
            {
                return found;
            }

            // Fallback match by display name
            if (spells != null)
            {
                for (int i = 0; i < spells.Count; i++)
                {
                    var s = spells[i];
                    if (s is not null && (string.Equals(s.spellId, spellId, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(s.displayName, spellId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return s;
                    }
                }
            }

            // If not found in database, fallback to canonical roster factory
            return AuthoritativeCombatRoster.GetSpell(spellId);
        }

        public IReadOnlyDictionary<string, SpellData> GetAllSpells()
        {
            if (_lookup == null || _lookup.Count != (spells?.Count ?? 0))
            {
                BuildLookup();
            }
            return _lookup;
        }

        public List<SpellData> GetBluffPool()
        {
            var pool = new List<SpellData>();
            if (spells != null && spells.Count > 0)
            {
                for (int i = 0; i < spells.Count; i++)
                {
                    var s = spells[i];
                    if (s is not null && s.isBluffEligible && s.damageClass != DamageClass.Fallback && !s.isMoveSkip && !s.isForceFallback)
                    {
                        pool.Add(s);
                    }
                }
            }

            if (pool.Count == 0)
            {
                return AuthoritativeCombatRoster.GetBluffEligiblePool();
            }

            return pool;
        }

        public List<SpellData> GetGuildLoadout(string guildId)
        {
            var loadout = new List<SpellData>(6);
            string gid = guildId?.ToUpperInvariant() ?? "MAGMA";

            if (gid.Contains("MAGMA"))
            {
                loadout.Add(GetSpell("FIREBALL"));
                loadout.Add(GetSpell("FIRE_CLOAK"));
                loadout.Add(GetSpell("FROZEN_HEAT"));
                loadout.Add(GetSpell("VOLCANIC_ASTEROID"));
                loadout.Add(GetSpell("ROYAL_INFERNO"));
                loadout.Add(GetSpell("STRUGGLE"));
            }
            else if (gid.Contains("AQUA"))
            {
                loadout.Add(GetSpell("SPROUT"));
                loadout.Add(GetSpell("SURF"));
                loadout.Add(GetSpell("QUICK_FREEZE"));
                loadout.Add(GetSpell("STEAM"));
                loadout.Add(GetSpell("BLIZZARD"));
                loadout.Add(GetSpell("STRUGGLE"));
            }
            else if (gid.Contains("PLASMA"))
            {
                loadout.Add(GetSpell("GUST"));
                loadout.Add(GetSpell("LIGHTNING"));
                loadout.Add(GetSpell("ELECTRIC_BULLET"));
                loadout.Add(GetSpell("ELECTRO_SWAMP"));
                loadout.Add(GetSpell("FLASHING_LIGHT"));
                loadout.Add(GetSpell("STRUGGLE"));
            }
            else if (gid.Contains("HOLY") || gid.Contains("TRINITY"))
            {
                loadout.Add(GetSpell("BLIND"));
                loadout.Add(GetSpell("BAPTISE"));
                loadout.Add(GetSpell("BURN_LIFE"));
                loadout.Add(GetSpell("ROYAL_INFERNO"));
                loadout.Add(GetSpell("DEATH_LIGHT"));
                loadout.Add(GetSpell("STRUGGLE"));
            }
            else if (gid.Contains("GAMBIT"))
            {
                loadout.Add(GetSpell("FIREBALL"));
                loadout.Add(GetSpell("LIGHTNING"));
                loadout.Add(GetSpell("SURF"));
                loadout.Add(GetSpell("BLIZZARD"));
                loadout.Add(GetSpell("SUFFOCATING_GAS"));
                loadout.Add(GetSpell("BLUFF"));
            }
            else
            {
                loadout.Add(GetSpell("FIREBALL"));
                loadout.Add(GetSpell("QUICK_FREEZE"));
                loadout.Add(GetSpell("BLIZZARD"));
                loadout.Add(GetSpell("SURF"));
                loadout.Add(GetSpell("GUST"));
                loadout.Add(GetSpell("STRUGGLE"));
            }

            return loadout;
        }

        /// <summary>
        /// Populates or updates this database from the canonical V1 roster.
        /// Useful in Editor or for initial project setup.
        /// </summary>
        [ContextMenu("Seed From Canonical Roster")]
        public void PopulateFromCanonicalRoster()
        {
            if (spells == null) spells = new List<SpellData>();
            foreach (var kvp in AuthoritativeCombatRoster.Spells)
            {
                int existingIndex = spells.FindIndex(s => s != null && string.Equals(s.spellId, kvp.Key, StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                {
                    spells[existingIndex] = kvp.Value;
                }
                else
                {
                    spells.Add(kvp.Value);
                }
            }
            BuildLookup();
        }

        /// <summary>
        /// Allows registering or overriding spell definitions dynamically at runtime.
        /// </summary>
        public void RegisterOrUpdateSpell(SpellData spell)
        {
            if (spell is null || string.IsNullOrEmpty(spell.spellId)) return;
            if (spells == null) spells = new List<SpellData>();

            int existingIndex = spells.FindIndex(s => s != null && string.Equals(s.spellId, spell.spellId, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                spells[existingIndex] = spell;
            }
            else
            {
                spells.Add(spell);
            }
            BuildLookup();
        }
    }
}
