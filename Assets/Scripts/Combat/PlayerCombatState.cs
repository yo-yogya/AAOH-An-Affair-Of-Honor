using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.StatusEffects;

namespace ArGuildEncounter.Combat
{
    [Serializable]
    public class ActiveStatusInstance
    {
        public StatusEffectData data;
        public int remainingTurns;
        public int currentStacks;

        public ActiveStatusInstance(StatusEffectData data)
        {
            this.data = data;
            this.remainingTurns = data != null ? data.durationTurns : 1;
            this.currentStacks = 1;
        }
    }

    /// <summary>
    /// Tracks authoritative HP, Mana, Spell Loadout, and active status effects for a combatant.
    /// Values are data-driven and clamped according to CombatConfig.
    /// </summary>
    public class PlayerCombatState
    {
        public string PlayerId { get; }
        public GuildData Guild { get; set; }

        public int MaxHP { get; private set; }
        public int CurrentHP { get; private set; }
        public int MaxMana { get; private set; }
        public int CurrentMana { get; private set; }
        public int ManaRegenPerTurn { get; private set; }

        public bool IsAlive => CurrentHP > 0;

        // Path Selection & Archetype Payload
        public PathDefinition AssignedPath { get; private set; }
        public float PathAttackBonus => (object)AssignedPath != null ? AssignedPath.payload.attackMultiplierBonus : 0f;
        public float PathDamageReduction => (object)AssignedPath != null ? AssignedPath.payload.damageReductionModifier : 0f;
        public int PathManaRegenBonus => (object)AssignedPath != null ? AssignedPath.payload.bonusManaRegenPerTurn : 0;

        // 6 Spell Slots: Slots 1-5 = Player spells; Slot 6 = Struggle or Bluff
        public SpellData[] SpellSlots { get; } = new SpellData[6];
        public SpellData FallbackAbility => SpellSlots[5];

        [Header("Authoritative Control Mechanics (COMBAT-BALANCE-V1)")]
        public bool IsMoveSkipActive { get; private set; }
        public bool IsForceFallbackActive { get; private set; }
        public int ControlImmunityActionsRemaining { get; private set; }
        public bool IsControlImmune => ControlImmunityActionsRemaining > 0;

        [Header("Status Buffs / Debuffs")]
        public int EmberWardTurnsRemaining { get; private set; } // +20% Fire offense, 2 turns
        public int FlameLegionChargesRemaining { get; private set; } // +15% Fire offense, 3 attacks
        public int HydrationSurgeChargesRemaining { get; private set; } // +15% Water offense, 2 attacks
        public int BlindnessAttacksRemaining { get; private set; } // +15% miss, 1 attack
        public int DampenedSightAttacksRemaining { get; private set; } // +10% miss, 1 attack
        public int SuffocationAttacksRemaining { get; private set; } // +10% miss, 1 attack

        private readonly List<ActiveStatusInstance> _activeStatuses = new List<ActiveStatusInstance>();
        public IReadOnlyList<ActiveStatusInstance> ActiveStatuses => _activeStatuses.AsReadOnly();

        public event Action<int, int> OnHPChanged; // (current, max)
        public event Action<int, int> OnManaChanged; // (current, max)
        public event Action OnDeath;
        public event Action<ActiveStatusInstance> OnStatusApplied;
        public event Action<ActiveStatusInstance> OnStatusRemoved;
        public event Action<string> OnControlStateChanged;

        public PlayerCombatState(string playerId, CombatConfig config, GuildData guild)
        {
            PlayerId = playerId;
            Guild = guild;

            int hp = (object)config != null ? config.defaultMaxHP : 1000;
            int mana = (object)config != null ? config.defaultMaxMana : 100;
            int regen = (object)config != null ? config.defaultManaRegenPerTurn : 10;

            MaxHP = hp;
            CurrentHP = hp;
            MaxMana = mana;
            CurrentMana = mana;
            ManaRegenPerTurn = regen;
        }

        public void SetLoadout(List<SpellData> spells, SpellData slot6Fallback)
        {
            for (int i = 0; i < 5; i++)
            {
                SpellSlots[i] = (spells != null && i < spells.Count) ? spells[i] : null;
            }
            SpellSlots[5] = slot6Fallback;
        }

        public void ResetCombatState()
        {
            CurrentHP = MaxHP;
            CurrentMana = MaxMana;
            IsMoveSkipActive = false;
            IsForceFallbackActive = false;
            ControlImmunityActionsRemaining = 0;
            EmberWardTurnsRemaining = 0;
            FlameLegionChargesRemaining = 0;
            HydrationSurgeChargesRemaining = 0;
            BlindnessAttacksRemaining = 0;
            DampenedSightAttacksRemaining = 0;
            SuffocationAttacksRemaining = 0;
            _activeStatuses.Clear();
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
            OnManaChanged?.Invoke(CurrentMana, MaxMana);
            OnControlStateChanged?.Invoke("RESET");
        }

        public void ApplyPathModifiers(PathDefinition path)
        {
            AssignedPath = path;
            if ((object)path == null) return;

            MaxHP = Mathf.Max(100, MaxHP + path.payload.maxHpModifier);
            CurrentHP = MaxHP;
            MaxMana = Mathf.Max(10, MaxMana + path.payload.maxManaModifier);
            CurrentMana = MaxMana;
            ManaRegenPerTurn += path.payload.bonusManaRegenPerTurn;

            if (path.startingSpellsOverride != null && path.startingSpellsOverride.Count > 0)
            {
                for (int i = 0; i < path.startingSpellsOverride.Count && i < 5; i++)
                {
                    if ((object)path.startingSpellsOverride[i] != null)
                    {
                        SpellSlots[i] = path.startingSpellsOverride[i];
                    }
                }
            }

            OnHPChanged?.Invoke(CurrentHP, MaxHP);
            OnManaChanged?.Invoke(CurrentMana, MaxMana);
        }

        public void ApplyDamage(int amount)
        {
            if (amount <= 0 || !IsAlive) return;

            int effectiveDamage = amount;
            if (PathDamageReduction > 0f)
            {
                effectiveDamage = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - PathDamageReduction)));
            }

            CurrentHP = Mathf.Clamp(CurrentHP - effectiveDamage, 0, MaxHP);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP == 0)
            {
                OnDeath?.Invoke();
            }
        }

        public void ApplyHealing(int amount)
        {
            if (amount <= 0 || !IsAlive) return;

            CurrentHP = Mathf.Clamp(CurrentHP + amount, 0, MaxHP);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        public void ApplySelfDamage(int amount)
        {
            if (amount <= 0 || !IsAlive) return;
            ApplyDamage(amount);
        }

        public void ApplySelfDamagePercent(float percentOfMaxHp)
        {
            if (percentOfMaxHp <= 0f || !IsAlive) return;
            int damage = Mathf.Max(1, Mathf.RoundToInt(MaxHP * percentOfMaxHp));
            ApplyDamage(damage);
        }

        public bool CanAffordMana(int cost)
        {
            return CurrentMana >= cost;
        }

        public bool TryDeductMana(int cost)
        {
            if (cost <= 0) return true;
            if (!CanAffordMana(cost)) return false;

            CurrentMana = Mathf.Clamp(CurrentMana - cost, 0, MaxMana);
            OnManaChanged?.Invoke(CurrentMana, MaxMana);
            return true;
        }

        public void DeductMana(int cost)
        {
            if (cost <= 0) return;
            CurrentMana = Mathf.Clamp(CurrentMana - cost, 0, MaxMana);
            OnManaChanged?.Invoke(CurrentMana, MaxMana);
        }

        /// <summary>
        /// Applies authoritative end-of-turn mana regeneration (+10).
        /// Per COMBAT-BALANCE-V1: Applied at the end of the acting player's turn if battle continues.
        /// </summary>
        public void RegenerateEndOfTurnMana()
        {
            if (!IsAlive || ManaRegenPerTurn <= 0) return;

            CurrentMana = Mathf.Clamp(CurrentMana + ManaRegenPerTurn, 0, MaxMana);
            OnManaChanged?.Invoke(CurrentMana, MaxMana);
        }

        // Backward compatibility
        public void RegenerateTurnMana()
        {
            RegenerateEndOfTurnMana();
        }

        /// <summary>
        /// Checks if any player-selected spell (slots 0-4) can be afforded with current mana.
        /// </summary>
        public bool HasAffordableSpellsInLoadout()
        {
            for (int i = 0; i < 5; i++)
            {
                if (SpellSlots[i] is not null && SpellSlots[i].manaCost <= CurrentMana)
                {
                    return true;
                }
            }
            return false;
        }

        public int Mana => CurrentMana;
        public bool HasForceFallback => IsForceFallbackActive;
        public bool HasMoveSkip => IsMoveSkipActive;
        public bool HasControlImmunity => IsControlImmune;

        /// <summary>
        /// Slot 6 (Struggle / Bluff) is selectable ONLY when no spells in slots 1-5 are affordable OR Force Fallback is active.
        /// </summary>
        public bool IsSlot6Selectable => !HasAffordableSpellsInLoadout() || IsForceFallbackActive;
        public bool IsSlot6SelectableMethod() => IsSlot6Selectable;

        /// <summary>
        /// Validates whether a spell in the given slot can be legally selected.
        /// Enforces Force Fallback, Slot 6 exclusivity, and Insufficient Mana rules.
        /// </summary>
        public bool CanCastSpell(int slotIndex, out string failureReason)
        {
            failureReason = "";
            if (slotIndex < 0 || slotIndex >= 6)
            {
                failureReason = "INVALID_SLOT";
                return false;
            }

            var spell = SpellSlots[slotIndex];
            if (spell is null)
            {
                failureReason = "EMPTY_SLOT";
                return false;
            }

            // 1. Force Fallback enforcement
            if (IsForceFallbackActive && slotIndex != 5)
            {
                failureReason = "FORCED FALLBACK ACTIVE: MUST USE SLOT 6";
                return false;
            }

            // 2. Slot 6 exclusivity (only when 0 moves affordable or Force Fallback active)
            if (slotIndex == 5 && !IsSlot6Selectable)
            {
                failureReason = "SLOT 6 ONLY SELECTABLE WHEN OUT OF MANA FOR SLOTS 1-5";
                return false;
            }

            // 3. Insufficient mana check
            if (spell.manaCost > CurrentMana)
            {
                failureReason = "INSUFFICIENT MANA";
                return false;
            }

            return true;
        }

        // =========================================================================
        // CONTROL MECHANICS (Move Skip, Force Fallback, Control Immunity)
        // =========================================================================

        public bool TryApplyMoveSkip()
        {
            if (IsControlImmune)
            {
                OnControlStateChanged?.Invoke("IMMUNE to Move Skip");
                return false;
            }

            if (IsMoveSkipActive) return false; // Does not stack

            IsMoveSkipActive = true;
            OnControlStateChanged?.Invoke("MOVE SKIP APPLIED");
            return true;
        }

        public bool TryApplyForceFallback()
        {
            if (IsControlImmune)
            {
                OnControlStateChanged?.Invoke("IMMUNE to Force Fallback");
                return false;
            }

            if (IsForceFallbackActive) return false; // Does not stack

            IsForceFallbackActive = true;
            OnControlStateChanged?.Invoke("FORCE FALLBACK APPLIED");
            return true;
        }

        public void ConsumeMoveSkip()
        {
            IsMoveSkipActive = false;
            GrantControlImmunity(1);
            OnControlStateChanged?.Invoke("Move Skip consumed -> Control Immunity [1 action]");
        }

        public void ConsumeForceFallback()
        {
            IsForceFallbackActive = false;
            GrantControlImmunity(1);
            OnControlStateChanged?.Invoke("Force Fallback consumed -> Control Immunity [1 action]");
        }

        public bool ConsumeMoveSkipIfActive()
        {
            if (IsMoveSkipActive)
            {
                ConsumeMoveSkip();
                return true;
            }
            return false;
        }

        public bool ConsumeForceFallbackIfActive()
        {
            if (IsForceFallbackActive)
            {
                ConsumeForceFallback();
                return true;
            }
            return false;
        }

        public bool CanCastSpell(SpellData spell, int slotIndex) => CanCastSpell(slotIndex, out _);

        public void GrantControlImmunity(int actions = 1)
        {
            ControlImmunityActionsRemaining = Mathf.Max(ControlImmunityActionsRemaining, actions);
        }

        public void TickControlImmunity()
        {
            if (ControlImmunityActionsRemaining > 0)
            {
                ControlImmunityActionsRemaining--;
            }
        }

        // =========================================================================
        // STATUS BUFFS & DEBUFFS (COMBAT-BALANCE-V1)
        // =========================================================================

        public void ApplyEmberWard()
        {
            EmberWardTurnsRemaining = 2; // Refreshes to 2 caster turns, does not stack
        }

        public void ApplyFlameLegion()
        {
            FlameLegionChargesRemaining = 3; // Refreshes to 3 attacks, does not stack
        }

        public void ApplyHydrationSurge()
        {
            HydrationSurgeChargesRemaining = 2; // Refreshes to 2 attacks, does not stack
        }

        public void ApplyBlindness()
        {
            BlindnessAttacksRemaining = 1; // +15% miss, 1 attack
        }

        public void ApplyDampenedSight()
        {
            DampenedSightAttacksRemaining = 1; // +10% miss, 1 attack
        }

        public void ApplySuffocation()
        {
            SuffocationAttacksRemaining = 1; // +10% miss, 1 attack
        }

        public float GetOffensiveBuffForElement(CombatElement element)
        {
            float bonus = 0f;
            if (element == CombatElement.Fire)
            {
                if (EmberWardTurnsRemaining > 0) bonus += 0.20f;
                if (FlameLegionChargesRemaining > 0) bonus += 0.15f;
            }
            if (element == CombatElement.Water)
            {
                if (HydrationSurgeChargesRemaining > 0) bonus += 0.15f;
            }
            return bonus;
        }

        /// <summary>
        /// Authoritatively applies or stacks a generic StatusEffectData instance.
        /// Handles duration refresh, stack limits, and dispatches OnStatusApplied.
        /// </summary>
        public void ApplyStatusEffect(StatusEffectData data)
        {
            if (data == null) return;

            ActiveStatusInstance existing = _activeStatuses.Find(s => s.data != null && s.data.statusId == data.statusId);
            if (existing != null)
            {
                switch (data.stackingRule)
                {
                    case StatusStackingRule.RefreshDuration:
                        existing.remainingTurns = Mathf.Max(existing.remainingTurns, data.durationTurns);
                        break;
                    case StatusStackingRule.StackDuration:
                        existing.remainingTurns += data.durationTurns;
                        break;
                    case StatusStackingRule.StackIntensity:
                        existing.currentStacks = Mathf.Min(existing.currentStacks + 1, Mathf.Max(1, data.maxStacks));
                        existing.remainingTurns = Mathf.Max(existing.remainingTurns, data.durationTurns);
                        break;
                    default:
                        existing.remainingTurns = Mathf.Max(existing.remainingTurns, data.durationTurns);
                        break;
                }
                OnStatusApplied?.Invoke(existing);
            }
            else
            {
                var newInstance = new ActiveStatusInstance(data);
                _activeStatuses.Add(newInstance);
                OnStatusApplied?.Invoke(newInstance);
            }
        }

        public bool RemoveStatusEffect(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return false;

            int index = _activeStatuses.FindIndex(s => s.data != null && s.data.statusId == statusId);
            if (index >= 0)
            {
                var removed = _activeStatuses[index];
                _activeStatuses.RemoveAt(index);
                OnStatusRemoved?.Invoke(removed);
                return true;
            }
            return false;
        }

        public void ClearStatuses(bool notify = true)
        {
            if (notify && OnStatusRemoved != null)
            {
                for (int i = 0; i < _activeStatuses.Count; i++)
                {
                    OnStatusRemoved.Invoke(_activeStatuses[i]);
                }
            }
            _activeStatuses.Clear();
        }

        public float GetTotalStatusMissModifier()
        {
            float total = 0f;
            if (BlindnessAttacksRemaining > 0) total += 0.15f;
            if (DampenedSightAttacksRemaining > 0) total += 0.10f;
            if (SuffocationAttacksRemaining > 0) total += 0.10f;

            for (int i = 0; i < _activeStatuses.Count; i++)
            {
                if (_activeStatuses[i]?.data != null)
                {
                    total += _activeStatuses[i].data.missModifier;
                }
            }
            return total;
        }

        public void ConsumeAttackBuffsAndDebuffs(bool attackSucceeded)
        {
            if (attackSucceeded)
            {
                if (FlameLegionChargesRemaining > 0) FlameLegionChargesRemaining--;
                if (HydrationSurgeChargesRemaining > 0) HydrationSurgeChargesRemaining--;
            }

            if (BlindnessAttacksRemaining > 0) BlindnessAttacksRemaining--;
            if (DampenedSightAttacksRemaining > 0) DampenedSightAttacksRemaining--;
            if (SuffocationAttacksRemaining > 0) SuffocationAttacksRemaining--;
        }

        public void TickStatusesTurnStart()
        {
            if (EmberWardTurnsRemaining > 0) EmberWardTurnsRemaining--;

            for (int i = _activeStatuses.Count - 1; i >= 0; i--)
            {
                var status = _activeStatuses[i];
                if (status == null || status.data == null)
                {
                    _activeStatuses.RemoveAt(i);
                    continue;
                }

                if (status.data.damagePerTurn > 0)
                {
                    int totalDot = status.data.damagePerTurn * Mathf.Max(1, status.currentStacks);
                    ApplyDamage(totalDot);
                }

                status.remainingTurns--;
                if (status.remainingTurns <= 0)
                {
                    _activeStatuses.RemoveAt(i);
                    OnStatusRemoved?.Invoke(status);
                }
            }
        }

        public string GetActiveStatusesDisplay()
        {
            var list = new List<string>();
            if (IsControlImmune) list.Add($"Control Immune [{ControlImmunityActionsRemaining}]");
            if (IsMoveSkipActive) list.Add("MOVE SKIP");
            if (IsForceFallbackActive) list.Add("FORCE FALLBACK");
            if (EmberWardTurnsRemaining > 0) list.Add($"Ember Ward [{EmberWardTurnsRemaining}T]");
            if (FlameLegionChargesRemaining > 0) list.Add($"Flame Legion [{FlameLegionChargesRemaining}A]");
            if (HydrationSurgeChargesRemaining > 0) list.Add($"Hydration Surge [{HydrationSurgeChargesRemaining}A]");
            if (BlindnessAttacksRemaining > 0) list.Add("Blind (+15% miss)");
            if (DampenedSightAttacksRemaining > 0) list.Add("Dampened (+10% miss)");
            if (SuffocationAttacksRemaining > 0) list.Add("Suffocation (+10% miss)");

            for (int i = 0; i < _activeStatuses.Count; i++)
            {
                if (_activeStatuses[i]?.data != null)
                {
                    list.Add($"{_activeStatuses[i].data.displayName} [{_activeStatuses[i].remainingTurns}T]");
                }
            }

            return list.Count > 0 ? string.Join(" | ", list) : "No active statuses";
        }
    }
}

