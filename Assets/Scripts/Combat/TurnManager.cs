using System;
using UnityEngine;

namespace ArGuildEncounter.Combat
{
    public class TurnStartedEventArgs : EventArgs
    {
        public int TurnNumber { get; }
        public string ActivePlayerId { get; }
        public bool IsHostTurn { get; }

        public TurnStartedEventArgs(int turnNumber, string activePlayerId, bool isHostTurn)
        {
            TurnNumber = turnNumber;
            ActivePlayerId = activePlayerId;
            IsHostTurn = isHostTurn;
        }
    }

    /// <summary>
    /// Manages alternating player turns with configurable first-player rules and per-turn maintenance.
    /// </summary>
    public class TurnManager
    {
        private readonly CombatConfig _config;
        private readonly string _hostPlayerId;
        private readonly string _clientPlayerId;
        private readonly PlayerCombatState _hostState;
        private readonly PlayerCombatState _clientState;

        public int CurrentTurnNumber { get; private set; }
        public string ActivePlayerId { get; private set; }
        public string WaitingPlayerId => ActivePlayerId == _hostPlayerId ? _clientPlayerId : _hostPlayerId;
        public bool IsHostTurn => ActivePlayerId == _hostPlayerId;

        public event EventHandler<TurnStartedEventArgs> TurnStarted;
        public event Action<int> TurnEnded;

        public TurnManager(
            CombatConfig config, 
            string hostPlayerId, 
            string clientPlayerId, 
            PlayerCombatState hostState, 
            PlayerCombatState clientState)
        {
            _config = config;
            _hostPlayerId = hostPlayerId;
            _clientPlayerId = clientPlayerId;
            _hostState = hostState;
            _clientState = clientState;
            CurrentTurnNumber = 0;
        }

        public void InitializeFirstTurn(Func<float> randomRoll = null)
        {
            CurrentTurnNumber = 1;
            FirstPlayerRule rule = (object)_config != null ? _config.firstPlayerRule : FirstPlayerRule.Random;

            switch (rule)
            {
                case FirstPlayerRule.HostFirst:
                    ActivePlayerId = _hostPlayerId;
                    break;
                case FirstPlayerRule.ClientFirst:
                    ActivePlayerId = _clientPlayerId;
                    break;
                case FirstPlayerRule.Random:
                default:
                    float roll = randomRoll != null ? randomRoll() : (float)new System.Random().NextDouble();
                    ActivePlayerId = roll >= 0.5f ? _hostPlayerId : _clientPlayerId;
                    break;
            }

            BeginTurnCycle();
        }

        public void AdvanceTurn()
        {
            TurnEnded?.Invoke(CurrentTurnNumber);
            CurrentTurnNumber++;
            ActivePlayerId = (ActivePlayerId == _hostPlayerId) ? _clientPlayerId : _hostPlayerId;
            BeginTurnCycle();
        }

        public void Reset()
        {
            CurrentTurnNumber = 0;
            ActivePlayerId = null;
        }

        private void BeginTurnCycle()
        {
            PlayerCombatState activeState = GetActivePlayerState();
            if (activeState != null && activeState.IsAlive)
            {
                // Status tick at turn start
                activeState.TickStatusesTurnStart();
            }

            TurnStarted?.Invoke(this, new TurnStartedEventArgs(CurrentTurnNumber, ActivePlayerId, IsHostTurn));
        }

        public PlayerCombatState GetActivePlayerState()
        {
            return ActivePlayerId == _hostPlayerId ? _hostState : _clientState;
        }

        public PlayerCombatState GetWaitingPlayerState()
        {
            return ActivePlayerId == _hostPlayerId ? _clientState : _hostState;
        }
    }
}
