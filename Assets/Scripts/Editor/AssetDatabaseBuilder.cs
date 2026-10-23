#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Environment;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Spells;
using TerrainData = ArGuildEncounter.Environment.TerrainData;

namespace ArGuildEncounter.EditorTools
{
    /// <summary>
    /// ASSET_DATABASE_BUILDER
    /// Authoritative editor tool to generate, populate, and validate all ScriptableObject data assets:
    /// - 30 SpellData assets
    /// - 5 GuildData assets
    /// - 10 TerrainData assets
    /// - TypeMatchupTable asset
    /// - SpellDatabase asset
    /// Idempotent, safe to run repeatedly, updates existing assets without breaking GUIDs.
    /// </summary>
    public static class AssetDatabaseBuilder
    {
        private const string SpellsFolder = "Assets/Data/Spells";
        private const string GuildsFolder = "Assets/Data/Guilds";
        private const string TerrainFolder = "Assets/Data/Terrain";
        private const string TypeSystemFolder = "Assets/Data/TypeSystem";

        [MenuItem("Tools/AR Guild Encounter/Generate and Validate All Data Assets")]
        public static void GenerateAndValidateAll()
        {
            Debug.Log("==================================================================");
            Debug.Log("[ASSET BUILDER] Starting Authoritative Data Asset Generation & Validation...");
            Debug.Log("==================================================================");

            EnsureDirectories();

            // 1. Spells
            var generatedSpells = GenerateSpellAssets();

            // 2. Guilds
            var generatedGuilds = GenerateGuildAssets();

            // 3. Terrain
            var generatedTerrains = GenerateTerrainAssets();

            // 4. Type Matchup Table
            var typeTable = GenerateTypeMatchupTable();

            // 5. Spell Database
            var spellDb = GenerateSpellDatabase(generatedSpells);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 6. Validate
            bool isValid = ValidateAll(generatedSpells, generatedGuilds, generatedTerrains, typeTable, spellDb);

            Debug.Log("==================================================================");
            Debug.Log(isValid
                ? "[ASSET BUILDER] ALL ASSETS SUCCESSFULLY GENERATED & VALIDATED (100% PASS)"
                : "[ASSET BUILDER] VALIDATION FAILED WITH ERRORS (CHECK CONSOLE)");
            Debug.Log("==================================================================");
        }

        private static void EnsureDirectories()
        {
            EnsureDirectory(SpellsFolder);
            EnsureDirectory(GuildsFolder);
            EnsureDirectory(TerrainFolder);
            EnsureDirectory(TypeSystemFolder);
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folder = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureDirectory(parent);
                }
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static List<SpellData> GenerateSpellAssets()
        {
            var result = new List<SpellData>();
            var canonical = AuthoritativeCombatRoster.Spells;

            foreach (var kvp in canonical)
            {
                var src = kvp.Value;
                string assetPath = $"{SpellsFolder}/Spell_{src.spellId}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<SpellData>(assetPath);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<SpellData>();
                    CopySpellData(src, asset);
                    AssetDatabase.CreateAsset(asset, assetPath);
                    Debug.Log($"-> Created Spell asset: {assetPath}");
                }
                else
                {
                    CopySpellData(src, asset);
                    EditorUtility.SetDirty(asset);
                }

                result.Add(asset);
            }

            return result;
        }

        private static void CopySpellData(SpellData src, SpellData dst)
        {
            dst.spellId = src.spellId;
            dst.spellName = src.spellName;
            dst.displayName = src.displayName;
            dst.type1 = src.type1;
            dst.type2 = src.type2;
            dst.isDualType = src.isDualType;
            dst.damageClass = src.damageClass;
            dst.castTier = src.castTier;
            dst.baseDamage = src.baseDamage;
            dst.manaCost = src.manaCost;
            dst.gestureId = src.gestureId;
            dst.castWindow = src.castWindow;
            dst.missModifier = src.missModifier;
            dst.healsCaster = src.healsCaster;
            dst.dealsSelfDamage = src.dealsSelfDamage;
            dst.isMoveSkip = src.isMoveSkip;
            dst.isForceFallback = src.isForceFallback;
            dst.statusEffectId = src.statusEffectId;
            dst.isCriticalEligible = src.isCriticalEligible;
            dst.isBluffEligible = src.isBluffEligible;
            dst.voiceAliases = src.voiceAliases != null ? new List<string>(src.voiceAliases) : new List<string>();
        }

        private static List<GuildData> GenerateGuildAssets()
        {
            var result = new List<GuildData>();
            var canonical = AuthoritativeCombatRoster.Guilds;

            foreach (var kvp in canonical)
            {
                var src = kvp.Value;
                string assetPath = $"{GuildsFolder}/Guild_{src.guildId}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<GuildData>(assetPath);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<GuildData>();
                    CopyGuildData(src, asset);
                    AssetDatabase.CreateAsset(asset, assetPath);
                    Debug.Log($"-> Created Guild asset: {assetPath}");
                }
                else
                {
                    CopyGuildData(src, asset);
                    EditorUtility.SetDirty(asset);
                }

                result.Add(asset);
            }

            return result;
        }

        private static void CopyGuildData(GuildData src, GuildData dst)
        {
            dst.guildId = src.guildId;
            dst.displayName = src.displayName;
            dst.guildType = src.guildType;
            dst.identityTheme = src.identityTheme;
            dst.affinityTypes = src.affinityTypes != null ? new List<CombatElement>(src.affinityTypes) : new List<CombatElement>();
        }

        private static List<TerrainData> GenerateTerrainAssets()
        {
            var result = new List<TerrainData>();
            var canonical = AuthoritativeCombatRoster.Terrains;

            foreach (var kvp in canonical)
            {
                var src = kvp.Value;
                string assetPath = $"{TerrainFolder}/Terrain_{src.terrainId}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<TerrainData>();
                    CopyTerrainData(src, asset);
                    AssetDatabase.CreateAsset(asset, assetPath);
                    Debug.Log($"-> Created Terrain asset: {assetPath}");
                }
                else
                {
                    CopyTerrainData(src, asset);
                    EditorUtility.SetDirty(asset);
                }

                result.Add(asset);
            }

            return result;
        }

        private static void CopyTerrainData(TerrainData src, TerrainData dst)
        {
            dst.terrainId = src.terrainId;
            dst.terrainName = src.terrainName;
            dst.primaryAffinity = src.primaryAffinity;
            dst.visualTheme = src.visualTheme;
        }

        private static TypeMatchupTable GenerateTypeMatchupTable()
        {
            string assetPath = $"{TypeSystemFolder}/TypeMatchupTable.asset";
            var table = AssetDatabase.LoadAssetAtPath<TypeMatchupTable>(assetPath);

            if (table == null)
            {
                table = ScriptableObject.CreateInstance<TypeMatchupTable>();
                table.PopulateDefaults();
                AssetDatabase.CreateAsset(table, assetPath);
                Debug.Log($"-> Created TypeMatchupTable asset: {assetPath}");
            }
            else
            {
                table.PopulateDefaults();
                EditorUtility.SetDirty(table);
            }

            return table;
        }

        private static SpellDatabaseSO GenerateSpellDatabase(List<SpellData> spells)
        {
            string assetPath = $"{SpellsFolder}/SpellDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<SpellDatabaseSO>(assetPath);

            if (db == null)
            {
                db = ScriptableObject.CreateInstance<SpellDatabaseSO>();
                db.spells = new List<SpellData>(spells);
                db.BuildLookup();
                AssetDatabase.CreateAsset(db, assetPath);
                Debug.Log($"-> Created SpellDatabase asset: {assetPath}");
            }
            else
            {
                db.spells = new List<SpellData>(spells);
                db.BuildLookup();
                EditorUtility.SetDirty(db);
            }

            return db;
        }

        private static bool ValidateAll(
            List<SpellData> spells,
            List<GuildData> guilds,
            List<TerrainData> terrains,
            TypeMatchupTable typeTable,
            SpellDatabaseSO spellDb)
        {
            bool allValid = true;

            // 1. Validate Spells
            if (spells.Count != 30)
            {
                Debug.LogError($"[VALIDATION] Expected 30 spells, found {spells.Count}!");
                allValid = false;
            }

            var spellIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in spells)
            {
                if (string.IsNullOrEmpty(s.spellId))
                {
                    Debug.LogError($"[VALIDATION] Spell with empty ID found: {s.name}");
                    allValid = false;
                }
                else if (!spellIds.Add(s.spellId))
                {
                    Debug.LogError($"[VALIDATION] Duplicate spell ID found: {s.spellId}");
                    allValid = false;
                }

                if (s.baseDamage <= 0 && s.damageClass != DamageClass.Fallback)
                {
                    Debug.LogError($"[VALIDATION] Spell {s.spellId} has invalid damage: {s.baseDamage}");
                    allValid = false;
                }
            }

            // 2. Validate Guilds
            if (guilds.Count != 5)
            {
                Debug.LogError($"[VALIDATION] Expected 5 guilds, found {guilds.Count}!");
                allValid = false;
            }

            // 3. Validate Terrains
            if (terrains.Count != 10)
            {
                Debug.LogError($"[VALIDATION] Expected 10 terrains, found {terrains.Count}!");
                allValid = false;
            }

            // 4. Validate Type Table
            if (typeTable == null || typeTable.Entries.Count == 0)
            {
                Debug.LogError("[VALIDATION] TypeMatchupTable has no entries!");
                allValid = false;
            }

            // 5. Validate Database
            if (spellDb == null || spellDb.spells.Count != 30)
            {
                Debug.LogError($"[VALIDATION] SpellDatabase does not contain 30 spells! Count: {spellDb?.spells?.Count ?? 0}");
                allValid = false;
            }

            return allValid;
        }
    }
}
#endif
