using System;
using System.Collections.Generic;
using ArGuildEncounter.Battle;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Developer and debug simulation controller.
    /// Allows driving the complete Battle State Machine without requiring physical AR devices,
    /// speech recognition hardware, or live network endpoints.
    /// </summary>
    public class BattleDebugSimulator
    {
        private readonly IBattleStateMachine _stateMachine;
        private readonly List<string> _simulationLogs = new List<string>();

        public IBattleStateMachine StateMachine => _stateMachine;
        public IReadOnlyList<string> SimulationLogs => _simulationLogs.AsReadOnly();

        public event Action<string> OnLogEmitted;

        public BattleDebugSimulator(IBattleStateMachine stateMachine)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _stateMachine.PhaseChanged += OnPhaseChanged;
        }

        private void OnPhaseChanged(object sender, BattlePhaseChangedEventArgs e)
        {
            Log($"[STATE EVENT] Transitioned: {e.PreviousPhase} -> {e.CurrentPhase}. Detail: {e.Reason}");
        }

        private void Log(string message)
        {
            _simulationLogs.Add(message);
            OnLogEmitted?.Invoke(message);
        }

        public bool SimulateCreateRoom(string roomCode = "123456")
        {
            Log($"Simulating Room Creation: {roomCode}");
            return _stateMachine.TryTransition(BattlePhase.RoomCreated, $"Room {roomCode} created");
        }

        public bool SimulatePlayerJoined(string opponentId = "Opponent_Debug")
        {
            Log($"Simulating Opponent Join: {opponentId}");
            return _stateMachine.TryTransition(BattlePhase.PlayerJoined, $"Opponent {opponentId} joined");
        }

        public bool SimulateARCalibrationComplete()
        {
            Log("Simulating AR Calibration: Spatial origin anchor established");
            return _stateMachine.TryTransition(BattlePhase.ARCalibration, "Calibrating shared battle-space") &&
                   _stateMachine.TryTransition(BattlePhase.GuildSelection, "Calibration complete");
        }

        public bool SimulateGuildSelected(string guildName = "Magma")
        {
            Log($"Simulating Guild Selection: {guildName}");
            return _stateMachine.TryTransition(BattlePhase.Loadout, $"Guild {guildName} confirmed");
        }

        public bool SimulateLoadoutReady()
        {
            Log("Simulating Spell Loadout: 5 spells + Slot 6 fallback ready");
            return _stateMachine.TryTransition(BattlePhase.BattleReady, "Loadout confirmed by both players");
        }

        public bool SimulateStartFirstTurn(string activePlayerId)
        {
            Log($"Simulating Battle Start: First turn granted to {activePlayerId}");
            return _stateMachine.TryTransition(BattlePhase.PlayerTurn, $"Turn started for {activePlayerId}");
        }

        public bool SimulateStartSpellSelection()
        {
            Log("Simulating Spell Selection Phase: Waiting for voice/tap input");
            return _stateMachine.TryTransition(BattlePhase.SpellSelection, "Player prompt: Summon spell");
        }

        public bool SimulateVoiceCommand(bool isRecognized, string spellName = "TEST_SPELL_1")
        {
            if (!_stateMachine.TryTransition(BattlePhase.VoiceProcessing, "Processing speech audio"))
            {
                return false;
            }

            if (isRecognized)
            {
                Log($"Simulating Voice SUCCESS: Recognized '{spellName}'");
                return _stateMachine.TryTransition(BattlePhase.SpellSelected, $"Summoned {spellName}");
            }
            else
            {
                Log("Simulating Voice FAILED: Audio command not recognized, prompting retry/tap fallback");
                return _stateMachine.TryTransition(BattlePhase.SpellSelection, "Voice unrecognized, returned to selection");
            }
        }

        public bool SimulateManualSpellTap(string spellName = "TEST_SPELL_1")
        {
            Log($"Simulating Manual Tap Fallback: Selected {spellName}");
            return _stateMachine.TryTransition(BattlePhase.SpellSelected, $"Manual tap selection: {spellName}");
        }

        public bool SimulateEnterGestureWindow()
        {
            Log("Simulating Gesture Window: Cast timer active (authoritative)");
            return _stateMachine.TryTransition(BattlePhase.GestureWindow, "Waiting for somatic hand gesture");
        }

        public bool SimulateGestureInput(bool isCorrectGesture, string gestureId = "GESTURE_FLAME_THRUST")
        {
            if (isCorrectGesture)
            {
                Log($"Simulating Gesture SUCCESS: Gesture {gestureId} validated");
                return _stateMachine.TryTransition(BattlePhase.AimPhase, "Gesture validated; enter aim phase");
            }
            else
            {
                Log($"Simulating Gesture FAILED: Gesture {gestureId} invalid. Turn will end and mana will be deducted.");
                // Cast failure ends turn per locked rule
                return _stateMachine.TryTransition(BattlePhase.AttackResolution, "Cast failed; mana penalty applied") &&
                       _stateMachine.TryTransition(BattlePhase.HPCheck, "Cast failed state check") &&
                       _stateMachine.TryTransition(BattlePhase.NextTurn, "Ending turn on cast failure");
            }
        }

        public bool SimulateCastTimeout()
        {
            Log("Simulating Cast TIMEOUT: Timer reached 0. Attack missed, mana deducted, turn ends.");
            return _stateMachine.TryTransition(BattlePhase.AttackResolution, "Cast timeout; attack missed") &&
                   _stateMachine.TryTransition(BattlePhase.HPCheck, "Timeout resolution") &&
                   _stateMachine.TryTransition(BattlePhase.NextTurn, "Passing turn due to timeout");
        }

        public bool SimulateAimAndLaunch(bool hasTargetLocked = true)
        {
            Log($"Simulating Aim & Attack Launch: Target locked = {hasTargetLocked}");
            return _stateMachine.TryTransition(BattlePhase.AttackLaunched, "Projectile launched at target hitbox");
        }

        public bool SimulateAttackResolution()
        {
            Log("Simulating Attack Resolution: Processing accuracy roll, modifiers, damage, and status");
            return _stateMachine.TryTransition(BattlePhase.AttackResolution, "Resolving hit/miss and damage");
        }

        public bool SimulateHPCheck(bool isOpponentDead = false)
        {
            if (!_stateMachine.TryTransition(BattlePhase.HPCheck, "Evaluating player & opponent HP"))
            {
                return false;
            }

            if (isOpponentDead)
            {
                Log("Simulating HP Check: Opponent HP reached 0. VICTORY!");
                return _stateMachine.TryTransition(BattlePhase.BattleEnd, "Match concluded: Target HP 0");
            }
            else
            {
                Log("Simulating HP Check: Both combatants alive. Advancing to NextTurn.");
                return _stateMachine.TryTransition(BattlePhase.NextTurn, "Advancing turn cycle");
            }
        }

        public bool SimulateAdvanceToNextTurn(string nextPlayerId)
        {
            Log($"Advancing to Next Turn for: {nextPlayerId}");
            return _stateMachine.TryTransition(BattlePhase.PlayerTurn, $"Turn passed to {nextPlayerId}");
        }

        public bool SimulateTrackingLost()
        {
            Log("Simulating AR Tracking Lost: Camera feed obscured or target lost tracking");
            return _stateMachine.TryTransition(BattlePhase.ARTrackingLost, "AR Tracking lost; paused for reacquisition");
        }

        public bool SimulateTrackingReacquired()
        {
            Log("Simulating AR Tracking Reacquired: Resuming battle from saved state");
            return _stateMachine.TryTransition(_stateMachine.StoredInterruptedPhase, "AR Tracking reacquired; battle resumed");
        }

        public bool SimulateTemporaryDisconnect()
        {
            Log("Simulating Temporary Disconnect: Socket dropped, attempting reconnect");
            return _stateMachine.TryTransition(BattlePhase.Reconnecting, "Network packet timeout; reconnecting...");
        }

        public bool SimulateReconnectSuccess()
        {
            Log("Simulating Reconnect Success: Synchronizing authoritative battle state");
            return _stateMachine.TryTransition(_stateMachine.StoredInterruptedPhase, "Reconnected successfully; state restored");
        }

        public bool SimulatePermanentOpponentAbandon()
        {
            Log("Simulating Opponent Teleported (Permanent abandon / timeout)");
            return _stateMachine.TryTransition(BattlePhase.OpponentTeleported, "Opponent disconnected permanently") &&
                   _stateMachine.TryTransition(BattlePhase.BattleEnd, "Awarding victory to remaining player");
        }
    }
}
