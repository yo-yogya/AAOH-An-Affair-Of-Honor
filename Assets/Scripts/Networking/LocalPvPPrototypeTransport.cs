using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ArGuildEncounter.Networking
{
    /// <summary>
    /// LOCAL_PVP_PROTOTYPE_TRANSPORT
    /// Same-LAN / Mobile Hotspot prototype network transport for Hackathon / Two-Device Testing.
    /// Uses local TCP streaming for authoritative combat and spatial updates,
    /// paired with local UDP broadcast discovery for private 6-digit room codes.
    /// Provides an explicit Manual Local IP Fallback in case router AP isolation blocks UDP broadcasts.
    /// NOT intended as final production cloud multiplayer (which will use Unity Relay / Lobby).
    /// </summary>
    public class LocalPvPPrototypeTransport : MonoBehaviour, INetworkSessionProvider
    {
        public const string TransportName = "LOCAL_PVP_PROTOTYPE_TRANSPORT";
        public const int DefaultTcpPort = 7777;
        public const int DefaultUdpDiscoveryPort = 7778;

        [Header("Transport Status")]
        [SerializeField] private NetworkConnectionState _state = NetworkConnectionState.Disconnected;
        [SerializeField] private string _localPlayerId = "Player_A";
        [SerializeField] private bool _isHost = true;
        [SerializeField] private float _networkRttMs = 0f;

        private NetworkRoomInfo _currentRoom;
        public NetworkConnectionState ConnectionState => _state;
        public NetworkRoomInfo CurrentRoom => _currentRoom;
        public string LocalPlayerId => _localPlayerId;
        public bool IsHost => _isHost;
        public float NetworkRttMs => _networkRttMs;
        public long TotalPacketsSent { get; private set; }
        public long TotalPacketsReceived { get; private set; }

        public event Action<NetworkConnectionState> ConnectionStateChanged;
        public event Action<NetworkRoomInfo> RoomUpdated;
        public event Action<string> PlayerConnected;
        public event Action<string> PlayerDisconnectedTemporary;
        public event Action<string> PlayerDisconnectedPermanent;

        // Custom PvP Sync Events
        public event Action<Vector3, Quaternion, AR.ARTrackingConfidence, float> OnRemoteSpatialReceived;
        public event Action<string> OnRemoteCombatActionReceived;
        public event Action<string> OnAuthoritativeCombatSyncReceived;
        public event Action<bool> OnPeerCalibrationConfirmedReceived;

        // Path Selection Sync Events
        public event Action<string, string> RemotePathIntentReceived;
        public event Action<string, bool> PeerPathStatusReceived;
        public event Action<string, string> AuthoritativePathFinalReceived;

        // Networking sockets
        private TcpListener _tcpListener;
        private TcpClient _tcpClient;
        private NetworkStream _netStream;
        private UdpClient _udpBeacon;
        private Thread _networkThread;
        private Thread _discoveryThread;
        private volatile bool _isRunning = false;
        private SynchronizationContext _mainThreadContext;

        private void Awake()
        {
            _mainThreadContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        private void OnDestroy()
        {
            LeaveRoom();
        }

        public void CreateRoom(string hostId, Action<bool, string> onComplete)
        {
            LeaveRoom();

            _localPlayerId = hostId;
            _isHost = true;
            SetState(NetworkConnectionState.CreatingRoom);

            // Generate 6-digit room code
            int codeNumber = new System.Random().Next(100000, 999999);
            string code = codeNumber.ToString("D6");
            _currentRoom = new NetworkRoomInfo(code, hostId);

            try
            {
                _isRunning = true;
                _tcpListener = new TcpListener(IPAddress.Any, DefaultTcpPort);
                _tcpListener.Start();

                // Start UDP Broadcast Beacon
                _discoveryThread = new Thread(RunHostUdpBeacon) { IsBackground = true };
                _discoveryThread.Start();

                // Start TCP Accept thread
                _networkThread = new Thread(RunHostTcpListener) { IsBackground = true };
                _networkThread.Start();

                SetState(NetworkConnectionState.RoomCreated);
                RoomUpdated?.Invoke(_currentRoom);

                Debug.Log($"[{TransportName}] Room {code} created on port {DefaultTcpPort}. Broadcasting UDP discovery beacon...");
                onComplete?.Invoke(true, code);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{TransportName}] CreateRoom error: {ex.Message}");
                SetState(NetworkConnectionState.Disconnected);
                onComplete?.Invoke(false, ex.Message);
            }
        }

        public void JoinRoom(string roomCode, string guestId, Action<bool, string> onComplete)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 6)
            {
                onComplete?.Invoke(false, "Invalid room code. Must be 6 digits.");
                return;
            }

            LeaveRoom();

            _localPlayerId = guestId;
            _isHost = false;
            SetState(NetworkConnectionState.JoiningRoom);

            // Run discovery asynchronously
            new Thread(() =>
            {
                string discoveredHostIp = DiscoverHostByRoomCode(roomCode, timeoutMs: 3500);
                if (!string.IsNullOrEmpty(discoveredHostIp))
                {
                    _mainThreadContext.Post(_ =>
                    {
                        ConnectToHostDirect(discoveredHostIp, DefaultTcpPort, roomCode, guestId, onComplete);
                    }, null);
                }
                else
                {
                    _mainThreadContext.Post(_ =>
                    {
                        Debug.LogWarning($"[{TransportName}] UDP discovery timed out for code {roomCode}. Manual IP entry available.");
                        onComplete?.Invoke(false, "UDP discovery timed out. Enter Host IP directly in Dev Mode.");
                    }, null);
                }
            }).Start();
        }

        public void ConnectWithManualIp(string hostIp, string roomCode, string guestId, Action<bool, string> onComplete)
        {
            LeaveRoom();
            _localPlayerId = guestId;
            _isHost = false;
            SetState(NetworkConnectionState.JoiningRoom);

            ConnectToHostDirect(hostIp, DefaultTcpPort, roomCode, guestId, onComplete);
        }

        private void ConnectToHostDirect(string hostIp, int port, string roomCode, string guestId, Action<bool, string> onComplete)
        {
            try
            {
                _isRunning = true;
                _tcpClient = new TcpClient();
                _tcpClient.Connect(hostIp, port);
                _netStream = _tcpClient.GetStream();

                _currentRoom = new NetworkRoomInfo(roomCode, "Host_Peer")
                {
                    guestPlayerId = guestId,
                    isGuestConnected = true
                };

                // Send Handshake
                SendPacket("HANDSHAKE", $"{guestId}|Guest");

                // Start receive thread
                _networkThread = new Thread(RunClientReceiver) { IsBackground = true };
                _networkThread.Start();

                SetState(NetworkConnectionState.Connected);
                RoomUpdated?.Invoke(_currentRoom);
                PlayerConnected?.Invoke("Host_Peer");

                Debug.Log($"[{TransportName}] Connected to host at {hostIp}:{port} for Room {roomCode}!");
                onComplete?.Invoke(true, $"Connected to host {hostIp}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{TransportName}] Direct connection to {hostIp} failed: {ex.Message}");
                SetState(NetworkConnectionState.Disconnected);
                onComplete?.Invoke(false, $"Connection failed: {ex.Message}");
            }
        }

        private string DiscoverHostByRoomCode(string roomCode, int timeoutMs)
        {
            using (UdpClient listener = new UdpClient())
            {
                try
                {
                    listener.Client.Bind(new IPEndPoint(IPAddress.Any, DefaultUdpDiscoveryPort));
                    listener.Client.ReceiveTimeout = timeoutMs;

                    IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    DateTime start = DateTime.UtcNow;

                    while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
                    {
                        try
                        {
                            byte[] bytes = listener.Receive(ref remoteEp);
                            string msg = Encoding.UTF8.GetString(bytes);
                            // Expected beacon: "AR_GUILD:<RoomCode>:<Port>"
                            if (msg.StartsWith("AR_GUILD:") && msg.Contains(roomCode))
                            {
                                Debug.Log($"[{TransportName}] Discovered host for room {roomCode} at {remoteEp.Address} via UDP beacon.");
                                return remoteEp.Address.ToString();
                            }
                        }
                        catch (SocketException)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{TransportName}] UDP discovery listener error: {ex.Message}");
                }
            }
            return null;
        }

        private void RunHostUdpBeacon()
        {
            try
            {
                using (UdpClient broadcaster = new UdpClient())
                {
                    broadcaster.EnableBroadcast = true;
                    IPEndPoint broadcastEp = new IPEndPoint(IPAddress.Broadcast, DefaultUdpDiscoveryPort);
                    string beacon = $"AR_GUILD:{_currentRoom?.roomCode}:{DefaultTcpPort}";
                    byte[] data = Encoding.UTF8.GetBytes(beacon);

                    while (_isRunning && _state == NetworkConnectionState.RoomCreated)
                    {
                        broadcaster.Send(data, data.Length, broadcastEp);
                        Thread.Sleep(800);
                    }
                }
            }
            catch { }
        }

        private void RunHostTcpListener()
        {
            try
            {
                while (_isRunning && _tcpListener != null)
                {
                    TcpClient client = _tcpListener.AcceptTcpClient();
                    _tcpClient = client;
                    _netStream = client.GetStream();

                    _mainThreadContext.Post(_ =>
                    {
                        if (_currentRoom != null)
                        {
                            _currentRoom.guestPlayerId = "Guest_Peer";
                            _currentRoom.isGuestConnected = true;
                        }
                        SetState(NetworkConnectionState.Connected);
                        RoomUpdated?.Invoke(_currentRoom);
                        PlayerConnected?.Invoke("Guest_Peer");
                    }, null);

                    // Read packets from client
                    ReadIncomingStream(_netStream);
                }
            }
            catch { }
        }

        private void RunClientReceiver()
        {
            if (_netStream != null)
            {
                ReadIncomingStream(_netStream);
            }
        }

        private void ReadIncomingStream(NetworkStream stream)
        {
            try
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (_isRunning && _tcpClient != null && _tcpClient.Connected)
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;

                        ProcessPacket(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{TransportName}] Stream disconnected: {ex.Message}");
            }
            finally
            {
                _mainThreadContext.Post(_ => HandleUnexpectedDrop(), null);
            }
        }

        private void ProcessPacket(string packetLine)
        {
            // Format: "PACKET_TYPE:PAYLOAD"
            int colonIndex = packetLine.IndexOf(':');
            if (colonIndex < 0) return;

            TotalPacketsReceived++;
            string type = packetLine.Substring(0, colonIndex);
            string payload = packetLine.Substring(colonIndex + 1);

            _mainThreadContext.Post(_ =>
            {
                switch (type)
                {
                    case "SPATIAL":
                        HandleSpatialPacket(payload);
                        break;
                    case "ACTION_INTENT":
                        OnRemoteCombatActionReceived?.Invoke(payload);
                        break;
                    case "COMBAT_SYNC":
                        OnAuthoritativeCombatSyncReceived?.Invoke(payload);
                        break;
                    case "CALIBRATION_SYNC":
                        OnPeerCalibrationConfirmedReceived?.Invoke(true);
                        break;
                    case "PATH_INTENT":
                        {
                            string[] parts = payload.Split('|');
                            if (parts.Length >= 2)
                            {
                                RemotePathIntentReceived?.Invoke(parts[0], parts[1]);
                            }
                        }
                        break;
                    case "PATH_STATUS":
                        {
                            string[] parts = payload.Split('|');
                            if (parts.Length >= 2 && bool.TryParse(parts[1], out bool isLocked))
                            {
                                PeerPathStatusReceived?.Invoke(parts[0], isLocked);
                            }
                        }
                        break;
                    case "PATH_FINAL":
                        {
                            string[] parts = payload.Split('|');
                            if (parts.Length >= 2)
                            {
                                AuthoritativePathFinalReceived?.Invoke(parts[0], parts[1]);
                            }
                        }
                        break;
                    case "HEARTBEAT":
                        SendPacket("HEARTBEAT_ACK", payload);
                        break;
                    case "HEARTBEAT_ACK":
                        if (float.TryParse(payload, out float sentTime))
                        {
                            float sample = Mathf.Max(0f, (Time.realtimeSinceStartup - sentTime) * 1000f);
                            _networkRttMs = _networkRttMs <= 0f ? sample : Mathf.Lerp(_networkRttMs, sample, 0.25f);
                        }
                        break;
                    case "DISCONNECT":
                        HandlePermanentAbandon();
                        break;
                }
            }, null);
        }

        private void HandleSpatialPacket(string payload)
        {
            // payload: "px,py,pz,ry,conf,time"
            string[] parts = payload.Split(',');
            if (parts.Length >= 5 &&
                float.TryParse(parts[0], out float px) &&
                float.TryParse(parts[1], out float py) &&
                float.TryParse(parts[2], out float pz) &&
                float.TryParse(parts[3], out float ry) &&
                int.TryParse(parts[4], out int confVal))
            {
                Vector3 sharedPos = new Vector3(px, py, pz);
                Quaternion sharedRot = Quaternion.Euler(0, ry, 0);
                AR.ARTrackingConfidence conf = (AR.ARTrackingConfidence)confVal;
                float ts = parts.Length > 5 && float.TryParse(parts[5], out float t) ? t : Time.time;

                OnRemoteSpatialReceived?.Invoke(sharedPos, sharedRot, conf, ts);
            }
        }

        public void SendPacket(string type, string payload)
        {
            if (_netStream == null || !_netStream.CanWrite) return;

            try
            {
                string line = $"{type}:{payload}\n";
                byte[] bytes = Encoding.UTF8.GetBytes(line);
                _netStream.Write(bytes, 0, bytes.Length);
                _netStream.Flush();
                TotalPacketsSent++;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{TransportName}] SendPacket error: {ex.Message}");
            }
        }

        public void SendSpatialUpdate(Vector3 sharedPos, Quaternion sharedRot, AR.ARTrackingConfidence confidence)
        {
            string payload = $"{sharedPos.x:F3},{sharedPos.y:F3},{sharedPos.z:F3},{sharedRot.eulerAngles.y:F1},{(int)confidence},{Time.time:F3}";
            SendPacket("SPATIAL", payload);
        }

        public void SendCombatActionIntent(string actionPayload)
        {
            SendPacket("ACTION_INTENT", actionPayload);
        }

        public void SendPathIntent(string playerId, string pathId)
        {
            SendPacket("PATH_INTENT", $"{playerId}|{pathId}");
        }

        public void BroadcastPathStatus(string playerId, bool isLockedIn)
        {
            SendPacket("PATH_STATUS", $"{playerId}|{isLockedIn}");
        }

        public void BroadcastPathFinal(string hostPathId, string guestPathId)
        {
            SendPacket("PATH_FINAL", $"{hostPathId}|{guestPathId}");
        }

        public void BroadcastCombatResolution(string resolutionPayload)
        {
            SendPacket("COMBAT_SYNC", resolutionPayload);
        }

        public void SendCalibrationConfirmed()
        {
            SendPacket("CALIBRATION_SYNC", "TRUE");
        }

        public void SendHeartbeatPing()
        {
            SendPacket("HEARTBEAT", Time.realtimeSinceStartup.ToString("F3"));
        }

        private void HandleUnexpectedDrop()
        {
            if (_state == NetworkConnectionState.Connected)
            {
                SetState(NetworkConnectionState.Reconnecting);
                PlayerDisconnectedTemporary?.Invoke("Network stream lost. Attempting to reconnect (15s grace)...");
            }
        }

        public void SimulateTemporaryDrop()
        {
            if (_state != NetworkConnectionState.Connected) return;
            SetState(NetworkConnectionState.Reconnecting);
            PlayerDisconnectedTemporary?.Invoke("Simulated temporary packet drop.");
        }

        public void SimulateReconnect()
        {
            if (_state != NetworkConnectionState.Reconnecting) return;
            SetState(NetworkConnectionState.Connected);
            RoomUpdated?.Invoke(_currentRoom);
        }

        public void SimulatePermanentAbandon()
        {
            SendPacket("DISCONNECT", "FORFEIT");
            HandlePermanentAbandon();
        }

        private void HandlePermanentAbandon()
        {
            SetState(NetworkConnectionState.PermanentlyDisconnected);
            PlayerDisconnectedPermanent?.Invoke("PLAYER TELEPORTED");
        }

        public void LeaveRoom()
        {
            _isRunning = false;

            try { _netStream?.Close(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            try { _tcpListener?.Stop(); } catch { }
            try { _udpBeacon?.Close(); } catch { }

            _netStream = null;
            _tcpClient = null;
            _tcpListener = null;
            _udpBeacon = null;

            _networkThread = null;
            _discoveryThread = null;

            _currentRoom = null;
            SetState(NetworkConnectionState.Disconnected);
        }

        /// <summary>
        /// Produces diagnostic text summarizing current socket state, throughput, and ping.
        /// </summary>
        public string GetConnectionDiagnostics()
        {
            string roomCode = _currentRoom != null ? _currentRoom.roomCode : "None";
            return $"[{TransportName}] State: {_state} | Role: {(_isHost ? "Host" : "Client")} | Room: {roomCode} | RTT: {_networkRttMs:F1}ms | TX: {TotalPacketsSent} | RX: {TotalPacketsReceived}";
        }

        private void SetState(NetworkConnectionState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                ConnectionStateChanged?.Invoke(_state);
            }
        }
    }
}
