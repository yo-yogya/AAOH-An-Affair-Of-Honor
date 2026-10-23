using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArGuildEncounter.Battle;
using ArGuildEncounter.CPU;
using ArGuildEncounter.Networking;
using ArGuildEncounter.Combat;

namespace ArGuildEncounter.UI
{
    /// <summary>
    /// MAIN_MENU_CONTROLLER
    /// Manages the top-level game flow screens:
    /// Mode Selection (PvP vs CPU), Room Creation (6-digit code), Room Joining,
    /// Waiting Room, and transitioning into AR calibration & match start.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _pvpSetupPanel;
        [SerializeField] private GameObject _waitingRoomPanel;
        [SerializeField] private GameObject _pathSelectionPanel;
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _resultPanel;

        [Header("Path Selection")]
        [SerializeField] private PathSelectionUIController _pathSelectionController;

        [Header("Main Menu Controls")]
        [SerializeField] private Button _btnPlayPvP;
        [SerializeField] private Button _btnPlayCpu;

        [Header("PvP Setup Controls")]
        [SerializeField] private Button _btnCreateRoom;
        [SerializeField] private Button _btnJoinRoom;
        [SerializeField] private TMP_InputField _inputRoomCode;
        [SerializeField] private Button _btnBackFromPvP;
        [SerializeField] private TextMeshProUGUI _txtPvPStatus;

        [Header("Waiting Room Controls")]
        [SerializeField] private TextMeshProUGUI _txtRoomCodeDisplay;
        [SerializeField] private TextMeshProUGUI _txtWaitingStatus;
        [SerializeField] private Button _btnStartWhenReady;
        [SerializeField] private Button _btnLeaveWaiting;

        [Header("Subsystem References")]
        private BattleCoordinator _coordinator;
        [SerializeField] private LocalPvPPrototypeTransport _transport;
        [SerializeField] private CPUController _cpuController;

        public event Action<bool> OnModeSelected; // true = PvP, false = CPU

        public void BindCoordinator(BattleCoordinator coordinator) => _coordinator = coordinator;

        private bool _isVsCpu = false;
        public bool IsVsCpu => _isVsCpu;

        private void Awake()
        {
            BindButtons();
            ShowPanel(PanelType.MainMenu);
        }

        private void Start()
        {
            var runner = FindAnyObjectByType<ArGuildEncounter.DebugSuite.GameFoundationRunner>();
            if (runner != null && runner.Coordinator != null && _coordinator == null)
            {
                BindCoordinator(runner.Coordinator);
            }
            if (_transport == null)
            {
                _transport = FindAnyObjectByType<LocalPvPPrototypeTransport>();
            }
            if (_cpuController == null)
            {
                _cpuController = FindAnyObjectByType<CPUController>();
            }
        }

        private void BindButtons()
        {
            if (_btnPlayPvP != null) _btnPlayPvP.onClick.AddListener(HandlePlayPvP);
            if (_btnPlayCpu != null) _btnPlayCpu.onClick.AddListener(HandlePlayCpu);

            if (_btnCreateRoom != null) _btnCreateRoom.onClick.AddListener(HandleCreateRoom);
            if (_btnJoinRoom != null) _btnJoinRoom.onClick.AddListener(HandleJoinRoom);
            if (_btnBackFromPvP != null) _btnBackFromPvP.onClick.AddListener(() => ShowPanel(PanelType.MainMenu));

            if (_btnStartWhenReady != null) _btnStartWhenReady.onClick.AddListener(HandleStartMatch);
            if (_btnLeaveWaiting != null) _btnLeaveWaiting.onClick.AddListener(HandleLeaveWaiting);
        }

        public void HandlePlayPvP()
        {
            _isVsCpu = false;
            if (_cpuController != null) _cpuController.enabled = false;
            OnModeSelected?.Invoke(true);
            ShowPanel(PanelType.PvPSetup);
            SetPvPStatus("Choose CREATE or JOIN a 6-digit room.");
        }

        public void HandlePlayCpu()
        {
            _isVsCpu = true;
            OnModeSelected?.Invoke(false);

            ShowPanel(PanelType.PathSelection);
            EnsurePathSelectionController();

            var runner = FindAnyObjectByType<ArGuildEncounter.DebugSuite.GameFoundationRunner>();
            if (runner != null)
            {
                runner.EnsureMatchInitialized();
            }

            if (_coordinator != null)
            {
                _coordinator.IsCpuOpponentEnabled = true;
            }

            _pathSelectionController.BeginSelection(
                _transport,
                _coordinator,
                "Player_1",
                "CPU_Opponent",
                isHost: true,
                (localPath, opponentPath) =>
                {
                    if (_cpuController != null) _cpuController.enabled = true;
                    LaunchBattleWithPaths(localPath, opponentPath);
                }
            );
        }

        public void HandleCreateRoom()
        {
            SetPvPStatus("Creating room and broadcasting beacon...");
            if (_transport != null)
            {
                _transport.CreateRoom("Player_Host", (success, code) =>
                {
                    if (success)
                    {
                        ShowPanel(PanelType.WaitingRoom);
                        if (_txtRoomCodeDisplay != null) _txtRoomCodeDisplay.text = $"ROOM CODE: {code}";
                        if (_txtWaitingStatus != null) _txtWaitingStatus.text = "Waiting for Player 2 to join...";
                        if (_btnStartWhenReady != null) _btnStartWhenReady.interactable = false;

                        _transport.PlayerConnected += HandlePlayerConnected;
                    }
                    else
                    {
                        SetPvPStatus($"Failed to create room: {code}");
                    }
                });
            }
        }

        public void HandleJoinRoom()
        {
            string code = _inputRoomCode != null ? _inputRoomCode.text.Trim() : "";
            if (code.Length != 6)
            {
                SetPvPStatus("Room code must be exactly 6 digits!");
                return;
            }

            SetPvPStatus($"Searching for room {code}...");
            if (_transport != null)
            {
                _transport.JoinRoom(code, "Player_Guest", (success, errorMsg) =>
                {
                    if (success)
                    {
                        ShowPanel(PanelType.WaitingRoom);
                        if (_txtRoomCodeDisplay != null) _txtRoomCodeDisplay.text = $"JOINED ROOM: {code}";
                        if (_txtWaitingStatus != null) _txtWaitingStatus.text = "Connected! Calibrating shared AR...";
                        if (_btnStartWhenReady != null) _btnStartWhenReady.interactable = true;
                    }
                    else
                    {
                        SetPvPStatus($"Failed to join: {errorMsg}");
                    }
                });
            }
        }

        private void HandlePlayerConnected(string guestId)
        {
            if (_txtWaitingStatus != null) _txtWaitingStatus.text = $"Player 2 ({guestId}) connected! Ready to duel.";
            if (_btnStartWhenReady != null) _btnStartWhenReady.interactable = true;
        }

        public void HandleStartMatch()
        {
            ShowPanel(PanelType.PathSelection);
            EnsurePathSelectionController();

            string localId = _transport != null ? _transport.LocalPlayerId : "Player_Host";
            string opponentId = (_transport != null && _transport.CurrentRoom != null && !string.IsNullOrEmpty(_transport.CurrentRoom.guestPlayerId))
                ? _transport.CurrentRoom.guestPlayerId
                : "Player_Guest";
            bool isHost = _transport != null ? _transport.IsHost : true;

            _pathSelectionController.BeginSelection(
                _transport,
                _coordinator,
                localId,
                opponentId,
                isHost,
                (localPath, opponentPath) =>
                {
                    LaunchBattleWithPaths(localPath, opponentPath);
                }
            );
        }

        public void LaunchBattleWithPaths(PathDefinition localPath, PathDefinition opponentPath)
        {
            ShowPanel(PanelType.HUD);
            var runner = FindAnyObjectByType<ArGuildEncounter.DebugSuite.GameFoundationRunner>();
            if (runner != null)
            {
                runner.EnsureMatchInitialized();
            }
            if (_coordinator != null)
            {
                _coordinator.AssignPlayerPaths(localPath, opponentPath);
                _coordinator.StartBattle(useDebugQuickStart: true);
            }
        }

        private void EnsurePathSelectionController()
        {
            if (_pathSelectionController == null)
            {
                _pathSelectionController = FindAnyObjectByType<PathSelectionUIController>();
                if (_pathSelectionController == null)
                {
                    _pathSelectionController = gameObject.AddComponent<PathSelectionUIController>();
                }
            }
        }

        public void HandleLeaveWaiting()
        {
            if (_transport != null)
            {
                _transport.PlayerConnected -= HandlePlayerConnected;
                _transport.LeaveRoom();
            }
            ShowPanel(PanelType.MainMenu);
        }

        public void ReturnToMainMenu()
        {
            var runner = FindAnyObjectByType<ArGuildEncounter.DebugSuite.GameFoundationRunner>();
            if (runner != null)
            {
                runner.ResetMatch();
            }
            else
            {
                if (_transport != null) _transport.LeaveRoom();
                if (_coordinator != null) _coordinator.ResetMatch();
            }
            ShowPanel(PanelType.MainMenu);
        }

        private void SetPvPStatus(string message)
        {
            if (_txtPvPStatus != null) _txtPvPStatus.text = message;
        }

        public enum PanelType { MainMenu, PvPSetup, WaitingRoom, PathSelection, HUD, Result }

        public void ShowPanel(PanelType type)
        {
            if (_mainMenuPanel != null) _mainMenuPanel.SetActive(type == PanelType.MainMenu);
            if (_pvpSetupPanel != null) _pvpSetupPanel.SetActive(type == PanelType.PvPSetup);
            if (_waitingRoomPanel != null) _waitingRoomPanel.SetActive(type == PanelType.WaitingRoom);
            if (_pathSelectionPanel != null) _pathSelectionPanel.SetActive(type == PanelType.PathSelection);
            if (_hudPanel != null) _hudPanel.SetActive(type == PanelType.HUD);
            if (_resultPanel != null) _resultPanel.SetActive(type == PanelType.Result);
        }
    }
}
