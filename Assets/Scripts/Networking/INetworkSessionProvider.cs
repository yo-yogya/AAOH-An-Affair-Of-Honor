using System;

namespace ArGuildEncounter.Networking
{
    public enum NetworkConnectionState
    {
        Disconnected = 0,
        CreatingRoom = 1,
        RoomCreated = 2,
        JoiningRoom = 3,
        Connected = 4,
        Reconnecting = 5,
        PermanentlyDisconnected = 6
    }

    [Serializable]
    public class NetworkRoomInfo
    {
        public string roomCode;
        public string hostPlayerId;
        public string guestPlayerId;
        public bool isGuestConnected;

        public string RoomCode => roomCode;
        public string HostPlayerId => hostPlayerId;

        public NetworkRoomInfo(string code, string hostId)
        {
            roomCode = code;
            hostPlayerId = hostId;
            guestPlayerId = "";
            isGuestConnected = false;
        }
    }

    /// <summary>
    /// Abstract network session interface.
    /// Decouples multiplayer transport logic from battle state and combat execution.
    /// Real networking (Netcode/WebSockets/Photon) can replace mock implementation without modifying battle code.
    /// </summary>
    public interface INetworkSessionProvider
    {
        NetworkConnectionState ConnectionState { get; }
        NetworkRoomInfo CurrentRoom { get; }
        string LocalPlayerId { get; }
        bool IsHost { get; }

        event Action<NetworkConnectionState> ConnectionStateChanged;
        event Action<NetworkRoomInfo> RoomUpdated;
        event Action<string> PlayerConnected;
        event Action<string> PlayerDisconnectedTemporary;
        event Action<string> PlayerDisconnectedPermanent;

        // Path Selection Sync Events
        event Action<string, string> RemotePathIntentReceived;
        event Action<string, bool> PeerPathStatusReceived;
        event Action<string, string> AuthoritativePathFinalReceived;

        void CreateRoom(string hostId, Action<bool, string> onComplete);
        void JoinRoom(string roomCode, string guestId, Action<bool, string> onComplete);
        void SendCombatActionIntent(string actionPayload);
        void SendPathIntent(string playerId, string pathId);
        void BroadcastPathStatus(string playerId, bool isLockedIn);
        void BroadcastPathFinal(string hostPathId, string guestPathId);
        void SimulateTemporaryDrop();
        void SimulateReconnect();
        void SimulatePermanentAbandon();
        void LeaveRoom();
    }
}
