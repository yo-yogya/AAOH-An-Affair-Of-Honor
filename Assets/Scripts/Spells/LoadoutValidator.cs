using System;
using System.Collections.Generic;
using ArGuildEncounter.Guilds;

namespace ArGuildEncounter.Spells
{
    public struct LoadoutValidationResult
    {
        public bool isValid;
        public string errorMessage;

        public static LoadoutValidationResult Success() => new LoadoutValidationResult { isValid = true, errorMessage = string.Empty };
        public static LoadoutValidationResult Failure(string error) => new LoadoutValidationResult { isValid = false, errorMessage = error };
    }

    /// <summary>
    /// Validates player spell loadouts per COMBAT-BALANCE-V1 Section 18.
    /// Rules:
    /// 1. Exactly 6 total slots.
    /// 2. Slots 1–5 are player-selected moves with no duplicate IDs.
    /// 3. Slots 1–5 must include at least one Low (L), one Medium (M), and one High (H) move.
    /// 4. Remaining 2 slots are flexible and may include SH moves.
    /// 5. Slot 6 is strictly a fallback: Struggle for non-Gambit guilds, Bluff for Gambit.
    /// </summary>
    public static class LoadoutValidator
    {
        public const int RequiredSlotCount = 6;
        public const int PlayerSelectableSlots = 5;
        public const int FallbackSlotIndex = 5; // 0-indexed 6th slot

        public static LoadoutValidationResult ValidateLoadout(IList<SpellData> spells, GuildData guild)
        {
            if (spells == null || spells.Count != RequiredSlotCount)
            {
                return LoadoutValidationResult.Failure($"Loadout must contain exactly {RequiredSlotCount} slots.");
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasL = false;
            bool hasM = false;
            bool hasH = false;

            for (int i = 0; i < PlayerSelectableSlots; i++)
            {
                SpellData spell = spells[i];
                if (spell is null)
                {
                    return LoadoutValidationResult.Failure($"Slot {i + 1} cannot be null or empty.");
                }

                if (string.IsNullOrEmpty(spell.spellId))
                {
                    return LoadoutValidationResult.Failure($"Slot {i + 1} has an invalid spell ID.");
                }

                if (seenIds.Contains(spell.spellId))
                {
                    return LoadoutValidationResult.Failure($"Duplicate spell detected: {spell.spellName} in Slot {i + 1}.");
                }
                seenIds.Add(spell.spellId);

                if (spell.damageClass == DamageClass.Fallback)
                {
                    return LoadoutValidationResult.Failure($"Slot {i + 1} cannot contain a fallback spell ({spell.spellName}).");
                }

                if (spell.damageClass == DamageClass.L) hasL = true;
                if (spell.damageClass == DamageClass.M) hasM = true;
                if (spell.damageClass == DamageClass.H) hasH = true;
            }

            if (!hasL) return LoadoutValidationResult.Failure("Loadout must contain at least one Low (L) damage move.");
            if (!hasM) return LoadoutValidationResult.Failure("Loadout must contain at least one Medium (M) damage move.");
            if (!hasH) return LoadoutValidationResult.Failure("Loadout must contain at least one High (H) damage move.");

            // Check slot 6 fallback
            SpellData slot6 = spells[FallbackSlotIndex];
            if (slot6 is null)
            {
                return LoadoutValidationResult.Failure("Slot 6 (fallback) cannot be null.");
            }

            bool isGambit = guild is not null && guild.guildId is not null && guild.guildId.ToLowerInvariant().Contains("gambit");
            if (isGambit)
            {
                if (!string.Equals(slot6.spellId, "BLUFF", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(slot6.spellName, "Bluff", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadoutValidationResult.Failure("Gambit guild must have Bluff in Slot 6.");
                }
            }
            else
            {
                if (!string.Equals(slot6.spellId, "STRUGGLE", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(slot6.spellName, "Struggle", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadoutValidationResult.Failure("Non-Gambit guilds must have Struggle in Slot 6.");
                }
            }

            return LoadoutValidationResult.Success();
        }
    }
}
