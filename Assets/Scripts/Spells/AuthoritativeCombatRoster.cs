using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Environment;
using ArGuildEncounter.Guilds;

namespace ArGuildEncounter.Spells
{
    /// <summary>
    /// AUTHORITATIVE SEEDER & TEST FACTORY for COMBAT-BALANCE-V1.
    /// 
    /// ROLE:
    /// This class acts as an in-memory importer, seeder, and test factory for all 30 canonical moves,
    /// 10 canonical competitive terrains, and 5 canonical guilds.
    /// It is NOT the permanent hardcoded production source of truth (which is data-driven via ScriptableObject
    /// assets or Addressables), but guarantees exact canonical data for development, testing, simulation,
    /// and runtime fallback when assets are not bound in the scene.
    /// </summary>
    public static class AuthoritativeCombatRoster
    {
        private static Dictionary<string, SpellData> _cachedSpells;
        private static Dictionary<string, TerrainData> _cachedTerrains;
        private static Dictionary<string, GuildData> _cachedGuilds;

        public static IReadOnlyDictionary<string, SpellData> Spells
        {
            get
            {
                if (_cachedSpells == null) InitializeRoster();
                return _cachedSpells;
            }
        }

        public static IReadOnlyDictionary<string, TerrainData> Terrains
        {
            get
            {
                if (_cachedTerrains == null) InitializeTerrains();
                return _cachedTerrains;
            }
        }

        public static IReadOnlyDictionary<string, GuildData> Guilds
        {
            get
            {
                if (_cachedGuilds == null) InitializeGuilds();
                return _cachedGuilds;
            }
        }

        public static SpellData GetSpell(string spellId)
        {
            if (Spells.TryGetValue(spellId, out var spell)) return spell;
            // Also try case-insensitive lookup
            foreach (var kvp in Spells)
            {
                if (string.Equals(kvp.Key, spellId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kvp.Value.spellName, spellId, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        public static TerrainData GetTerrain(string terrainId)
        {
            if (Terrains.TryGetValue(terrainId, out var terrain)) return terrain;
            foreach (var kvp in Terrains)
            {
                if (string.Equals(kvp.Key, terrainId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kvp.Value.terrainName, terrainId, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        public static GuildData GetGuild(string guildId)
        {
            if (Guilds.TryGetValue(guildId, out var guild)) return guild;
            foreach (var kvp in Guilds)
            {
                if (string.Equals(kvp.Key, guildId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kvp.Value.guildName, guildId, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns all canonical spells eligible for Gambit's Bluff selection pool.
        /// Excludes Flashing Light, Death Light, Struggle, Bluff, and any move with bluffEligible = false.
        /// </summary>
        public static List<SpellData> GetBluffEligiblePool()
        {
            var pool = new List<SpellData>();
            foreach (var spell in Spells.Values)
            {
                if (spell.bluffEligible && spell.damageClass != DamageClass.Fallback)
                {
                    pool.Add(spell);
                }
            }
            return pool;
        }

        /// <summary>
        /// Creates a legal 6-slot loadout matching Section 28 of COMBAT-BALANCE-V1.
        /// </summary>
        public static List<SpellData> GetSampleLoadout(string guildId)
        {
            var loadout = new List<SpellData>(6);
            string gid = guildId?.ToUpperInvariant() ?? "MAGMA";

            if (gid.Contains("MAGMA"))
            {
                loadout.Add(GetSpell("FIREBALL"));           // L (60)
                loadout.Add(GetSpell("FIRE_CLOAK"));         // L (60)
                loadout.Add(GetSpell("FROZEN_HEAT"));        // M (100)
                loadout.Add(GetSpell("VOLCANIC_ASTEROID"));  // H (140)
                loadout.Add(GetSpell("ROYAL_INFERNO"));      // SH (180)
                loadout.Add(GetSpell("STRUGGLE"));           // Fallback
            }
            else if (gid.Contains("AQUA"))
            {
                loadout.Add(GetSpell("SPROUT"));             // L (60)
                loadout.Add(GetSpell("SURF"));               // L (60)
                loadout.Add(GetSpell("QUICK_FREEZE"));       // M (100)
                loadout.Add(GetSpell("STEAM"));              // M (100)
                loadout.Add(GetSpell("BLIZZARD"));           // H (140)
                loadout.Add(GetSpell("STRUGGLE"));           // Fallback
            }
            else if (gid.Contains("PLASMA"))
            {
                loadout.Add(GetSpell("GUST"));               // M (100)
                loadout.Add(GetSpell("LIGHTNING"));          // M (100)
                loadout.Add(GetSpell("ELECTRIC_BULLET"));    // L (60)
                loadout.Add(GetSpell("ELECTRO_SWAMP"));      // H (140)
                loadout.Add(GetSpell("FLASHING_LIGHT"));     // H (140)
                loadout.Add(GetSpell("STRUGGLE"));           // Fallback
            }
            else if (gid.Contains("HOLY") || gid.Contains("TRINITY"))
            {
                loadout.Add(GetSpell("BLIND"));              // L (60)
                loadout.Add(GetSpell("BAPTISE"));            // M (100)
                loadout.Add(GetSpell("BURN_LIFE"));          // H (140)
                loadout.Add(GetSpell("ROYAL_INFERNO"));      // SH (180)
                loadout.Add(GetSpell("DEATH_LIGHT"));        // SH (180)
                loadout.Add(GetSpell("STRUGGLE"));           // Fallback
            }
            else if (gid.Contains("GAMBIT"))
            {
                loadout.Add(GetSpell("FIREBALL"));           // L (60)
                loadout.Add(GetSpell("LIGHTNING"));          // M (100)
                loadout.Add(GetSpell("SURF"));               // L (60)
                loadout.Add(GetSpell("BLIZZARD"));           // H (140)
                loadout.Add(GetSpell("SUFFOCATING_GAS"));    // H (140)
                loadout.Add(GetSpell("BLUFF"));              // Fallback
            }
            else
            {
                // Default fallback loadout
                loadout.Add(GetSpell("FIREBALL"));
                loadout.Add(GetSpell("QUICK_FREEZE"));
                loadout.Add(GetSpell("BLIZZARD"));
                loadout.Add(GetSpell("SURF"));
                loadout.Add(GetSpell("GUST"));
                loadout.Add(GetSpell("STRUGGLE"));
            }

            return loadout;
        }

        private static T CreateInstanceSafe<T>() where T : ScriptableObject, new()
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

        private static SpellData CreateSpell(
            string id, string name, DamageClass dmgClass, SpellTier castTier,
            int baseDamage, int manaCost, CombatElement type1,
            bool isDual = false, CombatElement type2 = CombatElement.Null,
            bool heals = false, bool selfDmg = false, int selfDmgAmt = 100,
            bool moveSkip = false, bool forceFallback = false,
            bool bluffElig = true, string[] aliases = null)
        {
            var spell = CreateInstanceSafe<SpellData>();
            spell.spellId = id;
            spell.spellName = name;
            spell.damageClass = dmgClass;
            spell.castTier = castTier;
            spell.tier = castTier; // keep legacy tier synchronized
            spell.baseDamage = baseDamage;
            spell.manaCost = manaCost;
            spell.element = type1;
            spell.type1 = type1;
            spell.isDualType = isDual;
            spell.type2 = type2;
            spell.healsCaster = heals;
            spell.dealsSelfDamage = selfDmg;
            spell.selfDamageAmount = selfDmgAmt;
            spell.isMoveSkip = moveSkip;
            spell.isForceFallback = forceFallback;
            spell.bluffEligible = bluffElig;
            spell.missModifier = 0.0f;
            spell.baseMissChance = 0.0f;
            spell.criticalEligible = true;
            spell.castWindow = (castTier == SpellTier.Tier1) ? 5.0f : ((castTier == SpellTier.Tier2) ? 4.0f : 3.0f);
            spell.castDurationSeconds = spell.castWindow;

            spell.gestureId = "GESTURE_THRUST";
            var aliasList = new List<string> { name.ToLowerInvariant(), id.ToLowerInvariant() };
            if (aliases != null) aliasList.AddRange(aliases);
            spell.voiceAliases = aliasList;

            return spell;
        }

        private static void InitializeRoster()
        {
            _cachedSpells = new Dictionary<string, SpellData>(StringComparer.OrdinalIgnoreCase);

            // 1. Fireball: Fire, L, 60 dmg, 12 mana, Tier1
            _cachedSpells["FIREBALL"] = CreateSpell("FIREBALL", "Fireball", DamageClass.L, SpellTier.Tier1, 60, 12, CombatElement.Fire);

            // 2. Fire Cloak: Fire, L, 60 dmg, 18 mana, Tier1 (Ember Ward)
            _cachedSpells["FIRE_CLOAK"] = CreateSpell("FIRE_CLOAK", "Fire Cloak", DamageClass.L, SpellTier.Tier1, 60, 18, CombatElement.Fire);

            // 3. Flame Legion: Fire, L, 60 dmg, 18 mana, Tier1 (Flame Legion)
            _cachedSpells["FLAME_LEGION"] = CreateSpell("FLAME_LEGION", "Flame Legion", DamageClass.L, SpellTier.Tier1, 60, 18, CombatElement.Fire);

            // 4. Royal Inferno: Fire + Light, SH, 180 dmg, 47 mana, Tier3
            _cachedSpells["ROYAL_INFERNO"] = CreateSpell("ROYAL_INFERNO", "Royal Inferno", DamageClass.SH, SpellTier.Tier3, 180, 47, CombatElement.Fire, true, CombatElement.Light);

            // 5. Frozen Heat: Fire + Ice, M, 100 dmg, 21 mana, Tier2
            _cachedSpells["FROZEN_HEAT"] = CreateSpell("FROZEN_HEAT", "Frozen Heat", DamageClass.M, SpellTier.Tier2, 100, 21, CombatElement.Fire, true, CombatElement.Ice);

            // 6. Volcanic Asteroid: Ground + Fire, H, 140 dmg, 27 mana, Tier2, 100 self-damage
            _cachedSpells["VOLCANIC_ASTEROID"] = CreateSpell("VOLCANIC_ASTEROID", "Volcanic Asteroid", DamageClass.H, SpellTier.Tier2, 140, 27, CombatElement.Ground, true, CombatElement.Fire, false, true, 100);

            // 7. Burn Life: Dark + Fire, H, 140 dmg, 37 mana, Tier2, 40% heal
            _cachedSpells["BURN_LIFE"] = CreateSpell("BURN_LIFE", "Burn Life", DamageClass.H, SpellTier.Tier2, 140, 37, CombatElement.Dark, true, CombatElement.Fire, true);

            // 8. Heal Lightning: Fire + Lightning, H, 140 dmg, 37 mana, Tier2, 40% heal
            _cachedSpells["HEAL_LIGHTNING"] = CreateSpell("HEAL_LIGHTNING", "Heal Lightning", DamageClass.H, SpellTier.Tier2, 140, 37, CombatElement.Fire, true, CombatElement.Lightning, true);

            // 9. Frozen Wood: Grass + Ice, H, 140 dmg, 31 mana, Tier2
            _cachedSpells["FROZEN_WOOD"] = CreateSpell("FROZEN_WOOD", "Frozen Wood", DamageClass.H, SpellTier.Tier2, 140, 31, CombatElement.Grass, true, CombatElement.Ice);

            // 10. Quick Freeze: Ice, M, 100 dmg, 18 mana, Tier2
            _cachedSpells["QUICK_FREEZE"] = CreateSpell("QUICK_FREEZE", "Quick Freeze", DamageClass.M, SpellTier.Tier2, 100, 18, CombatElement.Ice);

            // 11. Aurora Beam: Ice, M, 100 dmg, 14 mana, Tier2, 100 self-damage
            _cachedSpells["AURORA_BEAM"] = CreateSpell("AURORA_BEAM", "Aurora Beam", DamageClass.M, SpellTier.Tier2, 100, 14, CombatElement.Ice, false, CombatElement.Null, false, true, 100);

            // 12. Blizzard: Ice, H, 140 dmg, 28 mana, Tier2
            _cachedSpells["BLIZZARD"] = CreateSpell("BLIZZARD", "Blizzard", DamageClass.H, SpellTier.Tier2, 140, 28, CombatElement.Ice);

            // 13. Sprout: Water, L, 60 dmg, 12 mana, Tier1
            _cachedSpells["SPROUT"] = CreateSpell("SPROUT", "Sprout", DamageClass.L, SpellTier.Tier1, 60, 12, CombatElement.Water);

            // 14. Ground Fall: Ground, M, 100 dmg, 24 mana, Tier2 (Hydration Surge)
            _cachedSpells["GROUND_FALL"] = CreateSpell("GROUND_FALL", "Ground Fall", DamageClass.M, SpellTier.Tier2, 100, 24, CombatElement.Ground);

            // 15. Surf: Water, L, 60 dmg, 12 mana, Tier1
            _cachedSpells["SURF"] = CreateSpell("SURF", "Surf", DamageClass.L, SpellTier.Tier1, 60, 12, CombatElement.Water);

            // 16. Aqua Impact: Water + Ground, H, 140 dmg, 31 mana, Tier2
            _cachedSpells["AQUA_IMPACT"] = CreateSpell("AQUA_IMPACT", "Aqua Impact", DamageClass.H, SpellTier.Tier2, 140, 31, CombatElement.Water, true, CombatElement.Ground);

            // 17. Steam: Water + Fire, M, 100 dmg, 27 mana, Tier2, 40% heal
            _cachedSpells["STEAM"] = CreateSpell("STEAM", "Steam", DamageClass.M, SpellTier.Tier2, 100, 27, CombatElement.Water, true, CombatElement.Fire, true);

            // 18. Baptise: Light + Water, M, 100 dmg, 21 mana, Tier2
            _cachedSpells["BAPTISE"] = CreateSpell("BAPTISE", "Baptise", DamageClass.M, SpellTier.Tier2, 100, 21, CombatElement.Light, true, CombatElement.Water);

            // 19. Volt Wave: Lightning + Water, M, 100 dmg, 21 mana, Tier2
            _cachedSpells["VOLT_WAVE"] = CreateSpell("VOLT_WAVE", "Volt Wave", DamageClass.M, SpellTier.Tier2, 100, 21, CombatElement.Lightning, true, CombatElement.Water);

            // 20. Electro Swamp: Water + Dark, H, 140 dmg, 31 mana, Tier2 (Dampened Sight)
            _cachedSpells["ELECTRO_SWAMP"] = CreateSpell("ELECTRO_SWAMP", "Electro Swamp", DamageClass.H, SpellTier.Tier2, 140, 31, CombatElement.Water, true, CombatElement.Dark);

            // 21. Solar Beam: Lightning + Light, M, 100 dmg, 21 mana, Tier2
            _cachedSpells["SOLAR_BEAM"] = CreateSpell("SOLAR_BEAM", "Solar Beam", DamageClass.M, SpellTier.Tier2, 100, 21, CombatElement.Lightning, true, CombatElement.Light);

            // 22. Blind: Dark, L, 60 dmg, 18 mana, Tier1 (Blindness)
            _cachedSpells["BLIND"] = CreateSpell("BLIND", "Blind", DamageClass.L, SpellTier.Tier1, 60, 18, CombatElement.Dark);

            // 23. Flashing Light: Light, H, 140 dmg, 58 mana, Tier2, Move Skip (bluffEligible=false)
            _cachedSpells["FLASHING_LIGHT"] = CreateSpell("FLASHING_LIGHT", "Flashing Light", DamageClass.H, SpellTier.Tier2, 140, 58, CombatElement.Light, false, CombatElement.Null, false, false, 0, true, false, false);

            // 24. Gust: Wind, M, 100 dmg, 18 mana, Tier2
            _cachedSpells["GUST"] = CreateSpell("GUST", "Gust", DamageClass.M, SpellTier.Tier2, 100, 18, CombatElement.Wind);

            // 25. Suffocating Gas: Dark + Wind, H, 140 dmg, 31 mana, Tier2 (Suffocation)
            _cachedSpells["SUFFOCATING_GAS"] = CreateSpell("SUFFOCATING_GAS", "Suffocating Gas", DamageClass.H, SpellTier.Tier2, 140, 31, CombatElement.Dark, true, CombatElement.Wind);

            // 26. Electric Bullet: Lightning + Ground, L, 60 dmg, 15 mana, Tier1
            _cachedSpells["ELECTRIC_BULLET"] = CreateSpell("ELECTRIC_BULLET", "Electric Bullet", DamageClass.L, SpellTier.Tier1, 60, 15, CombatElement.Lightning, true, CombatElement.Ground);

            // 27. Lightning: Lightning, M, 100 dmg, 18 mana, Tier2
            _cachedSpells["LIGHTNING"] = CreateSpell("LIGHTNING", "Lightning", DamageClass.M, SpellTier.Tier2, 100, 18, CombatElement.Lightning);

            // 28. Death Light: Dark + Lightning, SH, 180 dmg, 61 mana, Tier3, Force Fallback (bluffEligible=false)
            _cachedSpells["DEATH_LIGHT"] = CreateSpell("DEATH_LIGHT", "Death Light", DamageClass.SH, SpellTier.Tier3, 180, 61, CombatElement.Dark, true, CombatElement.Lightning, false, false, 0, false, true, false);

            // 29. Struggle: Null, Fallback, 40 dmg, 0 mana, Tier1 (bluffEligible=false)
            _cachedSpells["STRUGGLE"] = CreateSpell("STRUGGLE", "Struggle", DamageClass.Fallback, SpellTier.Tier1, 40, 0, CombatElement.Null, false, CombatElement.Null, false, false, 0, false, false, false);

            // 30. Bluff: Null, Fallback, 0 dmg (dynamic 75%), 0 mana, Tier1 (bluffEligible=false)
            _cachedSpells["BLUFF"] = CreateSpell("BLUFF", "Bluff", DamageClass.Fallback, SpellTier.Tier1, 0, 0, CombatElement.Null, false, CombatElement.Null, false, false, 0, false, false, false);
        }

        private static void InitializeTerrains()
        {
            _cachedTerrains = new Dictionary<string, TerrainData>(StringComparer.OrdinalIgnoreCase);

            string[] names = {
                "Volcanic Ruins", "Frozen Expanse", "Tempest Sanctum", "Flooded Temple", "Verdant Wilds",
                "Celestial Court", "Umbral Hollow", "Null Nexus", "Skyreach", "Ancient Basin"
            };

            CombatElement[] affinities = {
                CombatElement.Fire, CombatElement.Ice, CombatElement.Lightning, CombatElement.Water, CombatElement.Grass,
                CombatElement.Light, CombatElement.Dark, CombatElement.Null, CombatElement.Wind, CombatElement.Ground
            };

            for (int i = 0; i < names.Length; i++)
            {
                var t = CreateInstanceSafe<TerrainData>();
                t.terrainId = names[i].Replace(" ", "_").ToUpperInvariant();
                t.terrainName = names[i];
                t.primaryAffinity = affinities[i];
                t.offensiveBonus = 0.25f;
                _cachedTerrains[t.terrainId] = t;
            }
        }

        private static void InitializeGuilds()
        {
            _cachedGuilds = new Dictionary<string, GuildData>(StringComparer.OrdinalIgnoreCase);

            // Magma: Fire, Ground
            var magma = CreateInstanceSafe<GuildData>();
            magma.guildId = "MAGMA";
            magma.guildName = "Magma";
            magma.affinityTypes = new List<CombatElement> { CombatElement.Fire, CombatElement.Ground };
            magma.offensiveBonus = 0.25f;
            magma.isGambitGuild = false;
            _cachedGuilds[magma.guildId] = magma;

            // Aqua: Water, Ice
            var aqua = CreateInstanceSafe<GuildData>();
            aqua.guildId = "AQUA";
            aqua.guildName = "Aqua";
            aqua.affinityTypes = new List<CombatElement> { CombatElement.Water, CombatElement.Ice };
            aqua.offensiveBonus = 0.25f;
            aqua.isGambitGuild = false;
            _cachedGuilds[aqua.guildId] = aqua;

            // Plasma: Lightning, Wind
            var plasma = CreateInstanceSafe<GuildData>();
            plasma.guildId = "PLASMA";
            plasma.guildName = "Plasma";
            plasma.affinityTypes = new List<CombatElement> { CombatElement.Lightning, CombatElement.Wind };
            plasma.offensiveBonus = 0.25f;
            plasma.isGambitGuild = false;
            _cachedGuilds[plasma.guildId] = plasma;

            // Holy Trinity: Light, Dark, Null
            var holy = CreateInstanceSafe<GuildData>();
            holy.guildId = "HOLY_TRINITY";
            holy.guildName = "Holy Trinity";
            holy.affinityTypes = new List<CombatElement> { CombatElement.Light, CombatElement.Dark, CombatElement.Null };
            holy.offensiveBonus = 0.25f;
            holy.isGambitGuild = false;
            _cachedGuilds[holy.guildId] = holy;

            // Gambit: No elemental bonus, 45% crit, 1.25x crit multiplier
            var gambit = CreateInstanceSafe<GuildData>();
            gambit.guildId = "GAMBIT";
            gambit.guildName = "Gambit";
            gambit.affinityTypes = new List<CombatElement>();
            gambit.offensiveBonus = 0f;
            gambit.isGambitGuild = true;
            gambit.gambitCritChance = 0.45f;
            gambit.gambitCritMultiplier = 1.25f;
            _cachedGuilds[gambit.guildId] = gambit;
        }
    }
}
