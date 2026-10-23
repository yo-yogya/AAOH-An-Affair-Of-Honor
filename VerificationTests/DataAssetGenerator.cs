using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Environment;

namespace VerificationTests
{
    public static class DataAssetGenerator
    {
        public const string SpellScriptGuid = "e8c32731f0029c047b0c0f5c285a7084";
        public const string GuildScriptGuid = "703dec12a8e670342be5208461ccd079";
        public const string TerrainScriptGuid = "c68748330d351b449b07bf23eef915fa";
        public const string TypeTableScriptGuid = "a1c8e3f4b5d67890123456789abcdef0";
        public const string SpellDbScriptGuid = "f17b91efb08542940b3c6933b0868b5f";

        public static bool GenerateAndValidate(string basePath, Action<string> log)
        {
            try
            {
                string dataRoot = Path.Combine(basePath, "Assets", "Data");
                string spellsDir = Path.Combine(dataRoot, "Spells");
                string guildsDir = Path.Combine(dataRoot, "Guilds");
                string terrainDir = Path.Combine(dataRoot, "Terrain");
                string typeSystemDir = Path.Combine(dataRoot, "TypeSystem");

                Directory.CreateDirectory(spellsDir);
                Directory.CreateDirectory(guildsDir);
                Directory.CreateDirectory(terrainDir);
                Directory.CreateDirectory(typeSystemDir);

                // Ensure folder .meta files
                EnsureFolderMeta(spellsDir);
                EnsureFolderMeta(guildsDir);
                EnsureFolderMeta(terrainDir);
                EnsureFolderMeta(typeSystemDir);

                var spellGuids = new Dictionary<string, string>();

                // 1. Generate 30 SpellData Assets
                var spells = AuthoritativeCombatRoster.Spells;
                int spellCount = 0;
                foreach (var kvp in spells)
                {
                    var spell = kvp.Value;
                    string assetName = $"Spell_{spell.spellId}";
                    string assetPath = Path.Combine(spellsDir, $"{assetName}.asset");
                    string metaPath = Path.Combine(spellsDir, $"{assetName}.asset.meta");

                    string assetGuid = DeterministicGuid($"Spell_{spell.spellId}");
                    spellGuids[spell.spellId] = assetGuid;

                    WriteSpellAsset(assetPath, spell);
                    WriteMetaFile(metaPath, assetGuid);
                    spellCount++;
                }
                log($"  -> Successfully generated {spellCount} SpellData assets in Assets/Data/Spells/");

                // 2. Generate 5 GuildData Assets
                var guilds = AuthoritativeCombatRoster.Guilds;
                int guildCount = 0;
                foreach (var kvp in guilds)
                {
                    var guild = kvp.Value;
                    string assetName = $"Guild_{guild.guildId}";
                    string assetPath = Path.Combine(guildsDir, $"{assetName}.asset");
                    string metaPath = Path.Combine(guildsDir, $"{assetName}.asset.meta");

                    string assetGuid = DeterministicGuid($"Guild_{guild.guildId}");
                    WriteGuildAsset(assetPath, guild);
                    WriteMetaFile(metaPath, assetGuid);
                    guildCount++;
                }
                log($"  -> Successfully generated {guildCount} GuildData assets in Assets/Data/Guilds/");

                // 3. Generate 10 TerrainData Assets
                var terrains = AuthoritativeCombatRoster.Terrains;
                int terrainCount = 0;
                foreach (var kvp in terrains)
                {
                    var terrain = kvp.Value;
                    string assetName = $"Terrain_{terrain.terrainId}";
                    string assetPath = Path.Combine(terrainDir, $"{assetName}.asset");
                    string metaPath = Path.Combine(terrainDir, $"{assetName}.asset.meta");

                    string assetGuid = DeterministicGuid($"Terrain_{terrain.terrainId}");
                    WriteTerrainAsset(assetPath, terrain);
                    WriteMetaFile(metaPath, assetGuid);
                    terrainCount++;
                }
                log($"  -> Successfully generated {terrainCount} TerrainData assets in Assets/Data/Terrain/");

                // 4. Generate TypeMatchupTable Asset
                string typeTablePath = Path.Combine(typeSystemDir, "TypeMatchupTable.asset");
                string typeTableMeta = Path.Combine(typeSystemDir, "TypeMatchupTable.asset.meta");
                string typeTableGuid = DeterministicGuid("TypeMatchupTable");
                WriteTypeMatchupTableAsset(typeTablePath);
                WriteMetaFile(typeTableMeta, typeTableGuid);
                log("  -> Successfully generated TypeMatchupTable asset in Assets/Data/TypeSystem/");

                // 5. Generate SpellDatabase Asset
                string spellDbPath = Path.Combine(spellsDir, "SpellDatabase.asset");
                string spellDbMeta = Path.Combine(spellsDir, "SpellDatabase.asset.meta");
                string spellDbGuid = DeterministicGuid("SpellDatabase");
                WriteSpellDatabaseAsset(spellDbPath, spellGuids);
                WriteMetaFile(spellDbMeta, spellDbGuid);
                log("  -> Successfully generated SpellDatabase asset linking all 30 canonical spells");

                // 6. Verification
                if (spellCount != 30 || guildCount != 5 || terrainCount != 10)
                {
                    log($"  ERROR: Asset count mismatch! Spells={spellCount}, Guilds={guildCount}, Terrains={terrainCount}");
                    return false;
                }

                if (!File.Exists(typeTablePath) || !File.Exists(spellDbPath))
                {
                    log("  ERROR: Core database/table asset files missing!");
                    return false;
                }

                log("  [PASSED] All 47 data assets and meta files verified on disk.");
                return true;
            }
            catch (Exception ex)
            {
                log($"  EXCEPTION in DataAssetGenerator: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private static void WriteSpellAsset(string path, SpellData spell)
        {
            var sb = new StringBuilder();
            sb.AppendLine("%YAML 1.1");
            sb.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            sb.AppendLine("--- !u!114 &11400000");
            sb.AppendLine("MonoBehaviour:");
            sb.AppendLine("  m_ObjectHideFlags: 0");
            sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
            sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
            sb.AppendLine("  m_GameObject: {fileID: 0}");
            sb.AppendLine("  m_Enabled: 1");
            sb.AppendLine("  m_EditorHideFlags: 0");
            sb.AppendLine($"  m_Script: {{fileID: 11500000, guid: {SpellScriptGuid}, type: 3}}");
            sb.AppendLine($"  m_Name: Spell_{spell.spellId}");
            sb.AppendLine("  m_EditorClassIdentifier: ArGuildEncounter.Spells:SpellData");
            sb.AppendLine($"  spellId: {spell.spellId}");
            sb.AppendLine($"  displayName: {spell.displayName}");
            sb.AppendLine($"  description: \"{spell.description}\"");
            sb.AppendLine($"  damageClass: {(int)spell.damageClass}");
            sb.AppendLine($"  castTier: {(int)spell.castTier}");
            sb.AppendLine($"  type1: {(int)spell.type1}");
            sb.AppendLine($"  isDualType: {(spell.isDualType ? 1 : 0)}");
            sb.AppendLine($"  type2: {(int)spell.type2}");
            sb.AppendLine("  voiceAliases:");
            if (spell.voiceAliases != null)
            {
                foreach (var alias in spell.voiceAliases)
                {
                    sb.AppendLine($"  - {alias}");
                }
            }
            sb.AppendLine($"  castWindow: {spell.castWindow}");
            sb.AppendLine($"  gestureId: {spell.gestureId}");
            sb.AppendLine($"  manaCost: {spell.manaCost}");
            sb.AppendLine($"  baseDamage: {spell.baseDamage}");
            sb.AppendLine($"  missModifier: {spell.missModifier}");
            sb.AppendLine($"  healsCaster: {(spell.healsCaster ? 1 : 0)}");
            sb.AppendLine($"  dealsSelfDamage: {(spell.dealsSelfDamage ? 1 : 0)}");
            sb.AppendLine($"  isMoveSkip: {(spell.isMoveSkip ? 1 : 0)}");
            sb.AppendLine($"  isForceFallback: {(spell.isForceFallback ? 1 : 0)}");
            sb.AppendLine($"  isCriticalEligible: {(spell.isCriticalEligible ? 1 : 0)}");
            sb.AppendLine($"  isBluffEligible: {(spell.isBluffEligible ? 1 : 0)}");

            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteGuildAsset(string path, GuildData guild)
        {
            var sb = new StringBuilder();
            sb.AppendLine("%YAML 1.1");
            sb.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            sb.AppendLine("--- !u!114 &11400000");
            sb.AppendLine("MonoBehaviour:");
            sb.AppendLine("  m_ObjectHideFlags: 0");
            sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
            sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
            sb.AppendLine("  m_GameObject: {fileID: 0}");
            sb.AppendLine("  m_Enabled: 1");
            sb.AppendLine("  m_EditorHideFlags: 0");
            sb.AppendLine($"  m_Script: {{fileID: 11500000, guid: {GuildScriptGuid}, type: 3}}");
            sb.AppendLine($"  m_Name: Guild_{guild.guildId}");
            sb.AppendLine("  m_EditorClassIdentifier: ArGuildEncounter.Guilds:GuildData");
            sb.AppendLine($"  guildId: {guild.guildId}");
            sb.AppendLine($"  displayName: {guild.displayName}");
            sb.AppendLine($"  guildType: {(int)guild.guildType}");
            sb.AppendLine($"  identityTheme: \"{guild.identityTheme}\"");
            sb.AppendLine("  affinityTypes:");
            if (guild.affinityTypes != null)
            {
                foreach (var aff in guild.affinityTypes)
                {
                    sb.AppendLine($"  - {(int)aff}");
                }
            }

            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteTerrainAsset(string path, TerrainData terrain)
        {
            var sb = new StringBuilder();
            sb.AppendLine("%YAML 1.1");
            sb.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            sb.AppendLine("--- !u!114 &11400000");
            sb.AppendLine("MonoBehaviour:");
            sb.AppendLine("  m_ObjectHideFlags: 0");
            sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
            sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
            sb.AppendLine("  m_GameObject: {fileID: 0}");
            sb.AppendLine("  m_Enabled: 1");
            sb.AppendLine("  m_EditorHideFlags: 0");
            sb.AppendLine($"  m_Script: {{fileID: 11500000, guid: {TerrainScriptGuid}, type: 3}}");
            sb.AppendLine($"  m_Name: Terrain_{terrain.terrainId}");
            sb.AppendLine("  m_EditorClassIdentifier: ArGuildEncounter.Environment:TerrainData");
            sb.AppendLine($"  terrainId: {terrain.terrainId}");
            sb.AppendLine($"  terrainName: {terrain.terrainName}");
            sb.AppendLine($"  primaryAffinity: {(int)terrain.primaryAffinity}");
            sb.AppendLine($"  visualTheme: \"{terrain.visualTheme}\"");

            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteTypeMatchupTableAsset(string path)
        {
            var entries = CombatConfig.CreateAuthoritativeTypeChart();
            var sb = new StringBuilder();
            sb.AppendLine("%YAML 1.1");
            sb.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            sb.AppendLine("--- !u!114 &11400000");
            sb.AppendLine("MonoBehaviour:");
            sb.AppendLine("  m_ObjectHideFlags: 0");
            sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
            sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
            sb.AppendLine("  m_GameObject: {fileID: 0}");
            sb.AppendLine("  m_Enabled: 1");
            sb.AppendLine("  m_EditorHideFlags: 0");
            sb.AppendLine($"  m_Script: {{fileID: 11500000, guid: {TypeTableScriptGuid}, type: 3}}");
            sb.AppendLine("  m_Name: TypeMatchupTable");
            sb.AppendLine("  m_EditorClassIdentifier: ArGuildEncounter.Combat:TypeMatchupTable");
            sb.AppendLine("  _entries:");
            foreach (var entry in entries)
            {
                sb.AppendLine($"  - attackingElement: {(int)entry.attackingElement}");
                sb.AppendLine($"    defendingElement: {(int)entry.defendingElement}");
                sb.AppendLine($"    multiplier: {entry.multiplier:F2}");
            }

            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteSpellDatabaseAsset(string path, Dictionary<string, string> spellGuids)
        {
            var sb = new StringBuilder();
            sb.AppendLine("%YAML 1.1");
            sb.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            sb.AppendLine("--- !u!114 &11400000");
            sb.AppendLine("MonoBehaviour:");
            sb.AppendLine("  m_ObjectHideFlags: 0");
            sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
            sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
            sb.AppendLine("  m_GameObject: {fileID: 0}");
            sb.AppendLine("  m_Enabled: 1");
            sb.AppendLine("  m_EditorHideFlags: 0");
            sb.AppendLine($"  m_Script: {{fileID: 11500000, guid: {SpellDbScriptGuid}, type: 3}}");
            sb.AppendLine("  m_Name: SpellDatabase");
            sb.AppendLine("  m_EditorClassIdentifier: ArGuildEncounter.Spells:SpellDatabaseSO");
            sb.AppendLine("  spells:");
            foreach (var kvp in spellGuids)
            {
                sb.AppendLine($"  - {{fileID: 11400000, guid: {kvp.Value}, type: 2}}");
            }

            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteMetaFile(string metaPath, string guid)
        {
            string content = $"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";
            File.WriteAllText(metaPath, content);
        }

        private static void EnsureFolderMeta(string dirPath)
        {
            string metaPath = $"{dirPath}.meta";
            if (!File.Exists(metaPath))
            {
                string folderName = Path.GetFileName(dirPath);
                string guid = DeterministicGuid($"Folder_{folderName}");
                string content = $"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";
                File.WriteAllText(metaPath, content);
            }
        }

        public static string DeterministicGuid(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(32);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
