using System;
using UnityEngine;

namespace ArGuildEncounter.Networking
{
    /// <summary>
    /// Mock implementation of INetworkSessionProvider.
    /// Provides deterministic 6-digit room code generation, local matchmaking,
    /// and disconnect/reconnect simulation for testing without external servers.
    /// </summary>
    public class MockNetworkSessionProvider : INetworkSessionProvider
    {
        private NetworkConnectionState _state = NetworkConnectionState.Disconnected;
        private NetworkRoomInfo _currentRoom;
        private string _localPlayerId = "Player_A";
        private bool _isHost = true;

        public NetworkConnectionState ConnectionState => _state;
        public NetworkRoomInfo CurrentRoom => _currentRoom;
        public string LocalPlayerId => _localPlayerId;
        public bool IsHost => _isHost;

        public event Action<NetworkConnectionState> ConnectionStateChanged;
        public event Action<NetworkRoomInfo> RoomUpdated;
        public event Action<string> PlayerConnected;
        public event Action<string> PlayerDisconnectedTemporary;
        public event Action<string> PlayerDisconnectedPermanent;

        // Path Selection Sync Events
        public event Action<string, string> RemotePathIntentReceived;
        public event Action<string, bool> PeerPathStatusReceived;
        public event Action<string, string> AuthoritativePathFinalReceived;

        public void CreateRoom(string hostId, Action<bool, string> onComplete)
        {
            _localPlayerId = hostId;
            _isHost = true;
            _state = NetworkConnectionState.CreatingRoom;
            ConnectionStateChanged?.Invoke(_state);

            // Generate deterministic 6-digit room code
            int codeNumber = new System.Random().Next(100000, 999999);
            string code = codeNumber.ToString("D6");

            _currentRoom = new NetworkRoomInfo(code, hostId);
            _state = NetworkConnectionState.RoomCreated;
            ConnectionStateChanged?.Invoke(_state);
            RoomUpdated?.Invoke(_currentRoom);

            onComplete?.Invoke(true, code);
        }

        public void JoinRoom(string roomCode, string guestId, Action<bool, string> onComplete)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 6)
            {
                onComplete?.Invoke(false, "Invalid room code. Code must be 6 digits.");
                return;
            }

            _localPlayerId = guestId;
            _isHost = false;
            _state = NetworkConnectionState.JoiningRoom;
            ConnectionStateChanged?.Invoke(_state);

            // If room doesn't exist yet, mock host pairing
            if (_currentRoom == null || _currentRoom.roomCode != roomCode)
            {
                _currentRoom = new NetworkRoomInfo(roomCode, "Host_Opponent");
            }

            _currentRoom.guestPlayerId = guestId;
            _currentRoom.isGuestConnected = true;

            _state = NetworkConnectionState.Connected;
            ConnectionStateChanged?.Invoke(_state);
            RoomUpdated?.Invoke(_currentRoom);
            PlayerConnected?.Invoke(_isHost ? guestId : _currentRoom.hostPlayerId);

            onComplete?.Invoke(true, "Successfully joined room " + roomCode);
        }

        public void SendCombatActionIntent(string actionPayload)
        {
            // In mock mode, action is received immediately by the local authoritative engine
        }

        public void SendPathIntent(string playerId, string pathId)
        {
            RemotePathIntentReceived?.Invoke(playerId, pathId);
        }

        public void BroadcastPathStatus(string playerId, bool isLockedIn)
        {
            PeerPathStatusReceived?.Invoke(playerId, isLockedIn);
        }

        public void BroadcastPathFinal(string hostPathId, string guestPathId)
        {
            AuthoritativePathFinalReceived?.Invoke(hostPathId, guestPathId);
        }

        public void SimulateTemporaryDrop()
        {
            if (_state != NetworkConnectionState.Connected) return;

            _state = NetworkConnectionState.Reconnecting;
            ConnectionStateChanged?.Invoke(_state);
            PlayerDisconnectedTemporary?.Invoke("Network packet timeout. Attempting to reconnect...");
        }

        public void SimulateReconnect()
        {
            if (_state != NetworkConnectionState.Reconnecting) return;

            _state = NetworkConnectionState.Connected;
            ConnectionStateChanged?.Invoke(_state);
            RoomUpdated?.Invoke(_currentRoom);
        }

        public void SimulatePermanentAbandon()
        {
            _state = NetworkConnectionState.PermanentlyDisconnected;
            ConnectionStateChanged?.Invoke(_state);
            PlayerDisconnectedPermanent?.Invoke("PLAYER TELEPORTED");
        }

        public void LeaveRoom()
        {
            _currentRoom = null;
            _state = NetworkConnectionState.Disconnected;
            ConnectionStateChanged?.Invoke(_state);
        }
    }
}
