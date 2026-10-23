using System.Collections.Generic;

namespace ArGuildEncounter.Spells
{
    /// <summary>
    /// Data-driven provider interface for combat spell definitions.
    /// Decouples the combat engine and coordinators from hardcoded roster implementations,
    /// enabling runtime, asset-based (ScriptableObject), or remote data configuration.
    /// </summary>
    public interface ISpellDataProvider
    {
        SpellData GetSpell(string spellId);
        IReadOnlyDictionary<string, SpellData> GetAllSpells();
        List<SpellData> GetBluffPool();
        List<SpellData> GetGuildLoadout(string guildId);
    }
}
