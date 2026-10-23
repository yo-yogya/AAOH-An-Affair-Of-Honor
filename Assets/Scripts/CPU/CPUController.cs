using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Battle;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.CPU
{
    public enum CpuDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>
    /// CPU_CONTROLLER
    /// Autonomous AI combat opponent.
    /// Operates strictly through the identical BattleCoordinator combat pipeline as human players.
    /// Evaluates mana, legal loadout, elemental matchups, lethal thresholds, and control mechanics.
    /// Never manipulates HP, Mana, or Turn states directly.
    /// </summary>
    public class CPUController : MonoBehaviour
    {
        [Header("AI Configuration")]
        [SerializeField] private CpuDifficulty _difficulty = CpuDifficulty.Normal;
        [SerializeField] private float _decisionDelaySeconds = 1.0f;
        [SerializeField] private float _gesturePerformDelaySeconds = 0.8f;
        [SerializeField] private float _aimHoldDelaySeconds = 0.6f;

        [Header("Coordinator Reference")]
        private BattleCoordinator _coordinator;

        public CpuDifficulty Difficulty
        {
            get => _difficulty;
            set => _difficulty = value;
        }

        public void Bind(BattleCoordinator coordinator)
        {
            if (_coordinator != null)
            {
                _coordinator.OnCpuTurnRequested -= HandleCpuTurnRequested;
            }

            _coordinator = coordinator;
            if (_coordinator != null)
            {
                _coordinator.IsCpuOpponentEnabled = true;
                _coordinator.OnCpuTurnRequested += HandleCpuTurnRequested;
            }
        }

        private void OnDestroy()
        {
            if (_coordinator != null)
            {
                _coordinator.OnCpuTurnRequested -= HandleCpuTurnRequested;
            }
        }

        private void HandleCpuTurnRequested()
        {
            if (!isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(ExecuteCpuTurnCoroutine());
        }

        private IEnumerator ExecuteCpuTurnCoroutine()
        {
            if (_coordinator == null) yield break;

            // 1. Natural thinking delay
            yield return new WaitForSeconds(_decisionDelaySeconds);

            if (_coordinator.StateMachine.CurrentPhase != BattlePhase.SpellSelection)
            {
                yield break;
            }

            PlayerCombatState cpuState = _coordinator.TurnManager.GetActivePlayerState();
            PlayerCombatState targetPlayer = _coordinator.TurnManager.GetWaitingPlayerState();

            if (cpuState == null || targetPlayer == null) yield break;

            // 2. Select optimal legal spell slot
            int chosenSlot = EvaluateBestMove(cpuState, targetPlayer);

            bool selectionSuccess = _coordinator.SelectSpellManualFallback(chosenSlot);
            if (!selectionSuccess)
            {
                // Fallback to slot 6 if restricted
                _coordinator.SelectSpellManualFallback(LoadoutValidator.FallbackSlotIndex);
            }

            // 3. Simulate Gesture Window
            yield return new WaitForSeconds(_gesturePerformDelaySeconds);

            if (_coordinator.StateMachine.CurrentPhase == BattlePhase.GestureWindow)
            {
                SpellData activeSpell = _coordinator.SelectedSpell;
                string gestureToPerform = activeSpell != null ? activeSpell.gestureId : "GESTURE_THRUST";

                // AI accuracy based on difficulty
                float confidence = _difficulty == CpuDifficulty.Easy ? 0.80f : 0.95f;
                _coordinator.GestureInput?.SimulateGesturePerformed(gestureToPerform, confidence);
            }

            // 4. Simulate Aim & Target Lock
            yield return new WaitForSeconds(_aimHoldDelaySeconds);

            if (_coordinator.StateMachine.CurrentPhase == BattlePhase.AimPhase)
            {
                // Aim directly at local opponent hitbox
                _coordinator.ARTracking?.SimulateTargetAcquisition(new Vector3(0f, 0f, -1.8f), 2.5f);
                _coordinator.CommitAttackLaunch();
            }
        }

        /// <summary>
        /// Public tactical recommendation utility exposing the AI's move evaluation logic.
        /// </summary>
        public int GetRecommendedMoveSlot(PlayerCombatState cpu, PlayerCombatState target)
        {
            if (cpu == null || target == null) return LoadoutValidator.FallbackSlotIndex;
            return EvaluateBestMove(cpu, target);
        }

        private int EvaluateBestMove(PlayerCombatState cpu, PlayerCombatState target)
        {
            // Force Fallback rule check
            if (cpu.HasForceFallback)
            {
                return LoadoutValidator.FallbackSlotIndex;
            }

            var slots = cpu.SpellSlots;
            int bestSlot = -1;
            float highestScore = -9999f;

            CombatElement defenderElement = (target.Guild != null && target.Guild.affinityTypes.Count > 0)
                ? target.Guild.affinityTypes[0]
                : CombatElement.Null;

            int affordableCount = 0;

            for (int i = 0; i < 5; i++)
            {
                if (i >= slots.Length) break;
                SpellData spell = slots[i];
                if ((object)spell == null) continue;

                if (spell.manaCost <= cpu.CurrentMana)
                {
                    affordableCount++;
                    float score = ScoreSpell(spell, cpu, target, defenderElement);
                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestSlot = i;
                    }
                }
            }

            // If no 1-5 move is affordable, must use Slot 6 (Struggle or Bluff)
            if (affordableCount == 0 || bestSlot == -1)
            {
                return LoadoutValidator.FallbackSlotIndex;
            }

            return bestSlot;
        }

        private float ScoreSpell(SpellData spell, PlayerCombatState cpu, PlayerCombatState target, CombatElement defenderElement)
        {
            float score = spell.baseDamage;

            // Element multiplier evaluation with null safety
            float mult = (_coordinator != null && _coordinator.Config != null)
                ? _coordinator.Config.GetDualTypeMultiplier(spell.type1, spell.type2, spell.isDualType, defenderElement)
                : 1.0f;
            score *= mult;

            // Guild affinity bonus with null safety
            if (cpu.Guild != null && cpu.Guild.HasAffinityBonus(spell))
            {
                float guildBonus = (_coordinator != null && _coordinator.Config != null)
                    ? _coordinator.Config.guildOffensiveBonus
                    : 0.25f;
                score *= (1f + guildBonus);
            }

            // Path offensive bonus scaling
            if (cpu.PathAttackBonus > 0f)
            {
                score *= (1f + cpu.PathAttackBonus);
            }

            // Lethal prioritization: account for defender's path damage reduction
            float effectiveDamageAgainstDefender = score;
            if (target.PathDamageReduction > 0f)
            {
                effectiveDamageAgainstDefender *= (1f - target.PathDamageReduction);
            }

            if (effectiveDamageAgainstDefender >= target.CurrentHP)
            {
                score += 5000f; // Decisive lethal move
            }

            // Healing prioritization if CPU is low on HP
            if (spell.healsCaster && cpu.CurrentHP < 400)
            {
                score += 300f;
            }

            // Control prioritization if target has high HP
            if (spell.isMoveSkip && target.CurrentHP > 500 && !target.IsControlImmune)
            {
                score += 250f;
            }

            // High difficulty minimizes mana waste
            if (_difficulty == CpuDifficulty.Hard)
            {
                score -= (spell.manaCost * 0.5f);
            }

            return score;
        }
    }
}
