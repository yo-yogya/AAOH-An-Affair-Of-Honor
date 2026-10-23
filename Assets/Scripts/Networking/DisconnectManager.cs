using System;
using ArGuildEncounter.Battle;

namespace ArGuildEncounter.Networking
{
    /// <summary>
    /// Manages network disconnect resilience according to specification.
    /// Distinguishes temporary disconnects (RECONNECTING...) from permanent abandonment (PLAYER TELEPORTED).
    /// </summary>
    public class DisconnectManager
    {
        private readonly IBattleStateMachine _stateMachine;
        private readonly INetworkSessionProvider _network;
        private readonly float _gracePeriodSeconds;

        public bool IsReconnecting { get; private set; }

        public event Action OnGracePeriodExpired;

        public DisconnectManager(
            IBattleStateMachine stateMachine, 
            INetworkSessionProvider network, 
            float gracePeriodSeconds = 15.0f)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _gracePeriodSeconds = gracePeriodSeconds;

            _network.PlayerDisconnectedTemporary += HandleTemporaryDisconnect;
            _network.ConnectionStateChanged += HandleConnectionStateChanged;
            _network.PlayerDisconnectedPermanent += HandlePermanentAbandon;
        }

        private void HandleTemporaryDisconnect(string reason)
        {
            IsReconnecting = true;
            _stateMachine.TryTransition(BattlePhase.Reconnecting, reason);
        }

        private void HandleConnectionStateChanged(NetworkConnectionState state)
        {
            if (state == NetworkConnectionState.Connected && IsReconnecting)
            {
                IsReconnecting = false;
                _stateMachine.TryTransition(_stateMachine.StoredInterruptedPhase, "Connection restored");
            }
        }

        private void HandlePermanentAbandon(string reason)
        {
            IsReconnecting = false;
            _stateMachine.TryTransition(BattlePhase.OpponentTeleported, reason);
            _stateMachine.TryTransition(BattlePhase.BattleEnd, "Awarding victory to remaining player");
        }

        public void ExpireGracePeriod()
        {
            if (IsReconnecting)
            {
                IsReconnecting = false;
                OnGracePeriodExpired?.Invoke();
                HandlePermanentAbandon("Reconnect grace period expired. PLAYER TELEPORTED.");
            }
        }
    }
}
