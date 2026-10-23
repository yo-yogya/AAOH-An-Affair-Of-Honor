using System;
using System.Collections.Generic;

namespace ArGuildEncounter.Battle
{
    /// <summary>
    /// Event data dispatched when the battle state machine transitions between phases.
    /// </summary>
    public class BattlePhaseChangedEventArgs : EventArgs
    {
        public BattlePhase PreviousPhase { get; }
        public BattlePhase CurrentPhase { get; }
        public string Reason { get; }
        public float Timestamp { get; }

        public BattlePhaseChangedEventArgs(BattlePhase previous, BattlePhase current, string reason, float timestamp)
        {
            PreviousPhase = previous;
            CurrentPhase = current;
            Reason = reason;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Interface for querying and listening to battle state transitions.
    /// </summary>
    public interface IBattleStateMachine
    {
        BattlePhase CurrentPhase { get; }
        BattlePhase PreviousPhase { get; }
        BattlePhase StoredInterruptedPhase { get; }
        bool IsInBattleLoop { get; }
        event EventHandler<BattlePhaseChangedEventArgs> PhaseChanged;

        bool AllowDebugQuickStart { get; set; }
        bool TryDebugQuickStart(string reason = "Debug quick-start");
        bool CanTransitionTo(BattlePhase targetPhase);
        bool TryTransition(BattlePhase targetPhase, string reason = "");
        void ForcePhase(BattlePhase targetPhase, string reason = "");
        IReadOnlyList<string> GetTransitionHistory();
    }

    /// <summary>
    /// Authoritative Battle State Machine implementing all mandatory phases and error/interrupt states.
    /// Supports resuming from temporary interruptions (ARTrackingLost, OpponentLost, Reconnecting)
    /// and routing permanent exits (OpponentTeleported, Forfeit, BattleEnd).
    /// </summary>
    public class BattleStateMachine : IBattleStateMachine
    {
        private BattlePhase _currentPhase;
        private BattlePhase _previousPhase;
        private BattlePhase _storedInterruptedPhase;
        private readonly List<string> _transitionHistory = new List<string>();
        private readonly Func<float> _timeProvider;

        public BattlePhase CurrentPhase => _currentPhase;
        public BattlePhase PreviousPhase => _previousPhase;
        public BattlePhase StoredInterruptedPhase => _storedInterruptedPhase;

        public bool IsInBattleLoop =>
            _currentPhase >= BattlePhase.PlayerTurn && _currentPhase <= BattlePhase.NextTurn;

        public event EventHandler<BattlePhaseChangedEventArgs> PhaseChanged;

        public BattleStateMachine(Func<float> timeProvider = null)
        {
            _timeProvider = timeProvider ?? (() => 0f);
            _currentPhase = BattlePhase.WaitingForPlayer;
            _previousPhase = BattlePhase.WaitingForPlayer;
            _storedInterruptedPhase = BattlePhase.WaitingForPlayer;
            LogTransition(_currentPhase, "Initial state");
        }

        public bool AllowDebugQuickStart { get; set; } = false;

        public bool TryDebugQuickStart(string reason = "Debug quick-start")
        {
            if (_currentPhase == BattlePhase.PlayerJoined)
            {
                ExecuteTransition(BattlePhase.BattleReady, $"DEBUG_QUICK_START: {reason}");
                return true;
            }
            return false;
        }

        public bool CanTransitionTo(BattlePhase targetPhase)
        {
            if (_currentPhase == targetPhase) return false;

            // Permanent terminal states
            if (_currentPhase == BattlePhase.BattleEnd)
            {
                return targetPhase == BattlePhase.WaitingForPlayer; // Reset to lobby
            }

            if (_currentPhase == BattlePhase.OpponentTeleported)
            {
                return targetPhase == BattlePhase.BattleEnd || targetPhase == BattlePhase.WaitingForPlayer;
            }

            // Any active combat or calibration phase can transition to transient error states
            if (targetPhase == BattlePhase.Reconnecting ||
                targetPhase == BattlePhase.ARTrackingLost ||
                targetPhase == BattlePhase.OpponentLost ||
                targetPhase == BattlePhase.OpponentTeleported ||
                targetPhase == BattlePhase.Forfeit)
            {
                return true;
            }

            // Resuming from transient states
            if (_currentPhase == BattlePhase.ARTrackingLost ||
                _currentPhase == BattlePhase.OpponentLost ||
                _currentPhase == BattlePhase.Reconnecting)
            {
                // Can resume back to where it was interrupted or advance to OpponentTeleported / Forfeit
                return targetPhase == _storedInterruptedPhase ||
                       targetPhase == BattlePhase.OpponentTeleported ||
                       targetPhase == BattlePhase.Forfeit ||
                       targetPhase == BattlePhase.BattleEnd;
            }

            // Normal sequential progression
            switch (_currentPhase)
            {
                case BattlePhase.WaitingForPlayer:
                    return targetPhase == BattlePhase.RoomCreated || targetPhase == BattlePhase.PlayerJoined;

                case BattlePhase.RoomCreated:
                    return targetPhase == BattlePhase.PlayerJoined;

                case BattlePhase.PlayerJoined:
                    if (AllowDebugQuickStart && targetPhase == BattlePhase.BattleReady)
                    {
                        return true;
                    }
                    return targetPhase == BattlePhase.PathSelection || targetPhase == BattlePhase.ARCalibration;

                case BattlePhase.PathSelection:
                    return targetPhase == BattlePhase.ARCalibration || 
                           targetPhase == BattlePhase.GuildSelection || 
                           targetPhase == BattlePhase.BattleReady || 
                           targetPhase == BattlePhase.WaitingForPlayer;

                case BattlePhase.ARCalibration:
                    return targetPhase == BattlePhase.GuildSelection;

                case BattlePhase.GuildSelection:
                    return targetPhase == BattlePhase.Loadout;

                case BattlePhase.Loadout:
                    return targetPhase == BattlePhase.BattleReady;

                case BattlePhase.BattleReady:
                    return targetPhase == BattlePhase.PlayerTurn;

                case BattlePhase.PlayerTurn:
                    return targetPhase == BattlePhase.SpellSelection;

                case BattlePhase.SpellSelection:
                    return targetPhase == BattlePhase.VoiceProcessing || 
                           targetPhase == BattlePhase.SpellSelected; // manual tap fallback

                case BattlePhase.VoiceProcessing:
                    return targetPhase == BattlePhase.SpellSelected || 
                           targetPhase == BattlePhase.SpellSelection; // retry voice or fallback

                case BattlePhase.SpellSelected:
                    return targetPhase == BattlePhase.GestureWindow;

                case BattlePhase.GestureWindow:
                    // Success -> AimPhase; Failure/Timeout -> AttackResolution or NextTurn directly
                    return targetPhase == BattlePhase.AimPhase || 
                           targetPhase == BattlePhase.AttackResolution ||
                           targetPhase == BattlePhase.NextTurn;

                case BattlePhase.AimPhase:
                    return targetPhase == BattlePhase.AttackLaunched || targetPhase == BattlePhase.NextTurn;

                case BattlePhase.AttackLaunched:
                    return targetPhase == BattlePhase.AttackResolution;

                case BattlePhase.AttackResolution:
                    return targetPhase == BattlePhase.HPCheck;

                case BattlePhase.HPCheck:
                    return targetPhase == BattlePhase.NextTurn || targetPhase == BattlePhase.BattleEnd;

                case BattlePhase.NextTurn:
                    return targetPhase == BattlePhase.PlayerTurn;

                case BattlePhase.Forfeit:
                    return targetPhase == BattlePhase.BattleEnd;

                default:
                    return false;
            }
        }

        public bool TryTransition(BattlePhase targetPhase, string reason = "")
        {
            if (!CanTransitionTo(targetPhase))
            {
                return false;
            }

            ExecuteTransition(targetPhase, reason);
            return true;
        }

        public void ForcePhase(BattlePhase targetPhase, string reason = "")
        {
            ExecuteTransition(targetPhase, $"FORCED: {reason}");
        }

        private void ExecuteTransition(BattlePhase targetPhase, string reason)
        {
            // If entering an interrupt state, store where we were so we can resume
            if (targetPhase == BattlePhase.ARTrackingLost ||
                targetPhase == BattlePhase.OpponentLost ||
                targetPhase == BattlePhase.Reconnecting)
            {
                if (_currentPhase != BattlePhase.ARTrackingLost &&
                    _currentPhase != BattlePhase.OpponentLost &&
                    _currentPhase != BattlePhase.Reconnecting)
                {
                    _storedInterruptedPhase = _currentPhase;
                }
            }

            _previousPhase = _currentPhase;
            _currentPhase = targetPhase;

            float time = _timeProvider();
            LogTransition(targetPhase, reason);

            PhaseChanged?.Invoke(this, new BattlePhaseChangedEventArgs(_previousPhase, _currentPhase, reason, time));
        }

        private void LogTransition(BattlePhase phase, string reason)
        {
            string entry = $"[{_timeProvider():0.00}s] {phase} (Reason: {(string.IsNullOrEmpty(reason) ? "Normal progression" : reason)})";
            _transitionHistory.Add(entry);
        }

        public IReadOnlyList<string> GetTransitionHistory() => _transitionHistory.AsReadOnly();
    }
}
