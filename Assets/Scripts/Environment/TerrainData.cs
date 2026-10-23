using System;
using UnityEngine;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.Environment
{
    public enum TerrainType
    {
        VolcanicRuins = 0,
        FrozenExpanse = 1,
        TempestSanctum = 2,
        FloodedTemple = 3,
        VerdantWilds = 4,
        CelestialCourt = 5,
        UmbralHollow = 6,
        NullNexus = 7,
        Skyreach = 8,
        AncientBasin = 9
    }

    /// <summary>
    /// Authoritative Terrain definition per COMBAT-BALANCE-V1.
    /// Selected at battle initialization and remains fixed for the entire match.
    /// Provides +25% offensive bonus to attacks matching its primary affinity.
    /// </summary>
    [CreateAssetMenu(fileName = "Terrain_New", menuName = "AR Guild Encounter/Terrain Data")]
    public class TerrainData : ScriptableObject
    {
        public TerrainType terrainType = TerrainType.VolcanicRuins;
        public string terrainId = "TERRAIN_VOLCANIC_RUINS";
        public string terrainName = "Volcanic Ruins";
        public CombatElement primaryAffinity = CombatElement.Fire;
        public float offensiveBonus = 0.25f;
        [TextArea(2, 4)]
        public string visualTheme = "Cracked obsidian, magma veins, embers floating in AR space.";
    }
}
