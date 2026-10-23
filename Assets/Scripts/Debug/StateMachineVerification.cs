using System;
using ArGuildEncounter.Battle;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Self-contained verification runner for the Battle State Machine and Debug Simulator.
    /// Can be executed in Unity Editor or standalone to prove state transitions.
    /// </summary>
    public class StateMachineVerification
    {
        public static bool RunVerification(Action<string> logAction)
        {
            var sm = new BattleStateMachine(() => 1.0f);
            var sim = new BattleDebugSimulator(sm);

            logAction("--- STARTING STATE MACHINE VERIFICATION ---");

            // 1. Initial State
            if (sm.CurrentPhase != BattlePhase.WaitingForPlayer)
            {
                logAction("FAIL: Initial state is not WaitingForPlayer");
                return false;
            }

            // 2. Room Creation
            if (!sim.SimulateCreateRoom("654321"))
            {
                logAction("FAIL: Could not transition to RoomCreated");
                return false;
            }

            // 3. Player Joined
            if (!sim.SimulatePlayerJoined("Player_2"))
            {
                logAction("FAIL: Could not transition to PlayerJoined");
                return false;
            }

            // 4. AR Calibration & Guild Selection
            if (!sim.SimulateARCalibrationComplete())
            {
                logAction("FAIL: AR Calibration flow failed");
                return false;
            }

            // 5. Guild Select & Loadout
            if (!sim.SimulateGuildSelected("Gambit") || !sim.SimulateLoadoutReady())
            {
                logAction("FAIL: Guild/Loadout confirmation failed");
                return false;
            }

            // 6. First Turn & Spell Selection
            if (!sim.SimulateStartFirstTurn("Player_1") || !sim.SimulateStartSpellSelection())
            {
                logAction("FAIL: Turn/Spell selection failed");
                return false;
            }

            // 7. Voice recognized & Gesture window
            if (!sim.SimulateVoiceCommand(true, "TEST_SPELL_1") || !sim.SimulateEnterGestureWindow())
            {
                logAction("FAIL: Voice command / Gesture window transition failed");
                return false;
            }

            // 8. Transient Interrupt: AR Tracking Lost and Reacquired
            if (!sim.SimulateTrackingLost())
            {
                logAction("FAIL: Tracking Lost interrupt failed");
                return false;
            }
            if (sm.CurrentPhase != BattlePhase.ARTrackingLost)
            {
                logAction("FAIL: Current state is not ARTrackingLost");
                return false;
            }
            if (!sim.SimulateTrackingReacquired())
            {
                logAction("FAIL: Tracking Reacquisition resume failed");
                return false;
            }
            if (sm.CurrentPhase != BattlePhase.GestureWindow)
            {
                logAction("FAIL: Failed to resume back to GestureWindow after tracking reacquisition");
                return false;
            }

            // 9. Gesture validation -> Aim -> Launch -> Resolution -> HP Check -> Next Turn
            if (!sim.SimulateGestureInput(true, "GESTURE_TEST") ||
                !sim.SimulateAimAndLaunch(true) ||
                !sim.SimulateAttackResolution() ||
                !sim.SimulateHPCheck(false) ||
                !sim.SimulateAdvanceToNextTurn("Player_2"))
            {
                logAction("FAIL: Combat loop cycle failed");
                return false;
            }

            // 10. Transient Interrupt: Reconnecting and Reconnect
            if (!sim.SimulateTemporaryDisconnect())
            {
                logAction("FAIL: Disconnect interrupt failed");
                return false;
            }
            if (!sim.SimulateReconnectSuccess())
            {
                logAction("FAIL: Reconnect resume failed");
                return false;
            }

            // 11. Opponent Abandon / Teleport -> BattleEnd
            if (!sim.SimulatePermanentOpponentAbandon())
            {
                logAction("FAIL: Opponent abandon handling failed");
                return false;
            }
            if (sm.CurrentPhase != BattlePhase.BattleEnd)
            {
                logAction("FAIL: Current state is not BattleEnd after opponent teleported");
                return false;
            }

            logAction("--- ALL STATE MACHINE TRANSITION TESTS PASSED ---");
            return true;
        }
    }
}
