#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using ArGuildEncounter.Combat;

namespace ArGuildEncounter.EditorTools
{
    /// <summary>
    /// Editor utility to generate and configure PathDefinition and CombatPathConfig assets.
    /// </summary>
    public static class PathAssetGenerator
    {
        private const string PathsFolder = "Assets/Data/Paths";

        [MenuItem("Tools/AR Guild Encounter/Generate Path Selection Assets")]
        public static void GeneratePathAssets()
        {
            if (!Directory.Exists(PathsFolder))
            {
                Directory.CreateDirectory(PathsFolder);
                AssetDatabase.Refresh();
            }

            // 1. Aggressive Path
            string aggroPath = $"{PathsFolder}/Path_Aggressive.asset";
            var aggro = AssetDatabase.LoadAssetAtPath<PathDefinition>(aggroPath);
            if (aggro == null)
            {
                aggro = ScriptableObject.CreateInstance<PathDefinition>();
                AssetDatabase.CreateAsset(aggro, aggroPath);
            }
            aggro.pathId = "AGGRESSIVE";
            aggro.displayName = "Aggressive";
            aggro.description = "Unleashes lethal offensive power (+20% Attack Bonus) at the sacrifice of resilience (-100 Max HP).";
            aggro.archetype = PathArchetype.Aggressive;
            aggro.themeColor = new Color(0.95f, 0.25f, 0.2f);
            aggro.payload = new PathStatPayload
            {
                maxHpModifier = -100,
                maxManaModifier = 0,
                attackMultiplierBonus = 0.20f,
                damageReductionModifier = 0.0f,
                bonusManaRegenPerTurn = 0,
                spawnOffset = new Vector3(0f, 0f, 0.35f)
            };
            EditorUtility.SetDirty(aggro);

            // 2. Defensive Path
            string defPath = $"{PathsFolder}/Path_Defensive.asset";
            var def = AssetDatabase.LoadAssetAtPath<PathDefinition>(defPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<PathDefinition>();
                AssetDatabase.CreateAsset(def, defPath);
            }
            def.pathId = "DEFENSIVE";
            def.displayName = "Defensive";
            def.description = "Fortified bastion build (+200 Max HP, +15% Damage Reduction) withstanding heavy assaults.";
            def.archetype = PathArchetype.Defensive;
            def.themeColor = new Color(0.2f, 0.6f, 0.95f);
            def.payload = new PathStatPayload
            {
                maxHpModifier = 200,
                maxManaModifier = 0,
                attackMultiplierBonus = 0.0f,
                damageReductionModifier = 0.15f,
                bonusManaRegenPerTurn = 0,
                spawnOffset = new Vector3(0f, 0f, -0.35f)
            };
            EditorUtility.SetDirty(def);

            // 3. Balanced Path
            string balPath = $"{PathsFolder}/Path_Balanced.asset";
            var bal = AssetDatabase.LoadAssetAtPath<PathDefinition>(balPath);
            if (bal == null)
            {
                bal = ScriptableObject.CreateInstance<PathDefinition>();
                AssetDatabase.CreateAsset(bal, balPath);
            }
            bal.pathId = "BALANCED";
            bal.displayName = "Balanced";
            bal.description = "Tactical equilibrium maintaining baseline vitals with enhanced spell energy recovery (+5 Mana Regen/turn).";
            bal.archetype = PathArchetype.Balanced;
            bal.themeColor = new Color(0.3f, 0.85f, 0.4f);
            bal.payload = new PathStatPayload
            {
                maxHpModifier = 0,
                maxManaModifier = 0,
                attackMultiplierBonus = 0.0f,
                damageReductionModifier = 0.0f,
                bonusManaRegenPerTurn = 5,
                spawnOffset = Vector3.zero
            };
            EditorUtility.SetDirty(bal);

            // 4. CombatPathConfig
            string configPath = $"{PathsFolder}/CombatPathConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<CombatPathConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CombatPathConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }
            config.selectionCountdownSeconds = 15.0f;
            config.availablePaths.Clear();
            config.availablePaths.Add(aggro);
            config.availablePaths.Add(def);
            config.availablePaths.Add(bal);
            config.defaultFallbackPath = bal;
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PATH GENERATOR] Generated 3 PathDefinitions and CombatPathConfig at '{PathsFolder}'.");
        }
    }
}
#endif
