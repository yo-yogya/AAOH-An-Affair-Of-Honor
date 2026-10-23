using System;
using UnityEngine;

namespace ArGuildEncounter.Environment
{
    public enum ArenaPreset
    {
        VolcanicRuins = 0,
        TempestSanctum = 1,
        FloodedTemple = 2,
        CelestialCourt = 3,
        VoidGardens = 4
    }

    /// <summary>
    /// Arena preset containing environmental reference, visual theme, audio, and modifiers.
    /// </summary>
    [CreateAssetMenu(fileName = "Arena_New", menuName = "AR Guild Encounter/Arena Data")]
    public class ArenaData : ScriptableObject
    {
        public ArenaPreset arenaPreset = ArenaPreset.VolcanicRuins;
        public string arenaId = "ARENA_VOLCANIC_RUINS";
        public string arenaName = "Volcanic Ruins";
        [TextArea(2, 4)]
        public string visualTheme = "Cracked obsidian, magma veins, embers floating in AR space.";

        [Header("Linked Environment")]
        public EnvironmentData defaultEnvironment;

        [Header("Presentation (Placeholders)")]
        public string arenaVfxPrefabName = "VFX_Arena_Volcanic";
        public string arenaLightingProfileName = "Lighting_Volcanic";
        public string backgroundMusicClipName = "BGM_VolcanicRuins";
    }
}
