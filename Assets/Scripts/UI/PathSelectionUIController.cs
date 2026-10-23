using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Battle;
using ArGuildEncounter.Networking;

namespace ArGuildEncounter.UI
{
    /// <summary>
    /// PATH_SELECTION_UI_CONTROLLER
    /// Authoritative pre-battle Path Selection manager.
    /// Manages 3 data-driven paths (Aggressive, Defensive, Balanced),
    /// authoritative 15s countdown timer with automatic default fallback,
    /// hidden opponent pick status for competitive fairness,
    /// network packet synchronization (Host authoritative source of truth),
    /// and graceful disconnect handling.
    /// </summary>
    public class PathSelectionUIController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private CombatPathConfig _config;
        public CombatPathConfig Config
        {
            get => _config;
            set => _config = value;
        }

        [Header("UI Panels & Indicators")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _txtTitle;
        [SerializeField] private TextMeshProUGUI _txtTimer;
        [SerializeField] private Slider _sliderTimer;
        [SerializeField] private TextMeshProUGUI _txtStatusBanner;
        [SerializeField] private TextMeshProUGUI _txtOpponentStatus;
        [SerializeField] private Button _btnLockIn;
        [SerializeField] private TextMeshProUGUI _txtLockIn;

        [Header("Path Cards (3 Selectable Options)")]
        [SerializeField] private Button[] _pathCards = new Button[3];
        [SerializeField] private TextMeshProUGUI[] _pathNames = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] _pathDescriptions = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] _pathStats = new TextMeshProUGUI[3];
        [SerializeField] private Image[] _pathHighlights = new Image[3];

        // Runtime Session Context
        private INetworkSessionProvider _network;
        private BattleCoordinator _coordinator;
        private string _localPlayerId = "Player_Local";
        private string _opponentPlayerId = "Player_Opponent";
        private bool _isHost = true;
        private Action<PathDefinition, PathDefinition> _onSelectionCompleted;

        // Selection State
        private int _selectedPathIndex = -1;
        private bool _isLocalLockedIn = false;
        private bool _isOpponentLockedIn = false;
        private string _hostChosenPathId = "";
        private string _guestChosenPathId = "";
        private Coroutine _countdownCoroutine;
        private float _timeRemaining = 15.0f;
        private bool _isPausedForReconnect = false;

        public bool IsSelectionActive { get; private set; } = false;

        private void Awake()
        {
            if (_config == null)
            {
                _config = CombatPathConfig.CreateDefaultConfig();
            }
            EnsureUIReferences();
        }

        /// <summary>
        /// Begins the authoritative Path Selection phase.
        /// </summary>
        public void BeginSelection(
            INetworkSessionProvider network,
            BattleCoordinator coordinator,
            string localPlayerId,
            string opponentPlayerId,
            bool isHost,
            Action<PathDefinition, PathDefinition> onComplete)
        {
            if (_config == null)
            {
                _config = CombatPathConfig.CreateDefaultConfig();
            }

            _network = network;
            _coordinator = coordinator;
            _localPlayerId = !string.IsNullOrEmpty(localPlayerId) ? localPlayerId : "Player_1";
            _opponentPlayerId = !string.IsNullOrEmpty(opponentPlayerId) ? opponentPlayerId : "Player_2";
            _isHost = isHost;
            _onSelectionCompleted = onComplete;

            _selectedPathIndex = 2; // Default to Balanced initially
            _isLocalLockedIn = false;
            _isOpponentLockedIn = false;
            _hostChosenPathId = "";
            _guestChosenPathId = "";
            _isPausedForReconnect = false;
            IsSelectionActive = true;

            HookNetworkEvents();
            PopulateCards();
            HighlightCard(_selectedPathIndex);

            if (_panelRoot != null) _panelRoot.SetActive(true);
            if (_txtStatusBanner != null) _txtStatusBanner.text = "CHOOSE YOUR PATH (Hidden from opponent)";
            if (_txtOpponentStatus != null) _txtOpponentStatus.text = "Opponent is choosing...";
            if (_btnLockIn != null) _btnLockIn.interactable = true;
            if (_txtLockIn != null) _txtLockIn.text = "LOCK IN PATH";

            // Start Authoritative Countdown
            _timeRemaining = _config != null ? _config.selectionCountdownSeconds : 15.0f;
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = StartCoroutine(CountdownRoutine());

            Debug.Log($"[PATH SELECTION] Started. Host: {_isHost}, Countdown: {_timeRemaining}s.");
        }

        private void HookNetworkEvents()
        {
            if (_network == null) return;

            _network.RemotePathIntentReceived += HandleRemotePathIntent;
            _network.PeerPathStatusReceived += HandlePeerPathStatus;
            _network.AuthoritativePathFinalReceived += HandleAuthoritativePathFinal;
            _network.PlayerDisconnectedTemporary += HandleDisconnectTemporary;
            _network.PlayerDisconnectedPermanent += HandleDisconnectPermanent;
            _network.ConnectionStateChanged += HandleConnectionStateChanged;
        }

        private void UnhookNetworkEvents()
        {
            if (_network == null) return;

            _network.RemotePathIntentReceived -= HandleRemotePathIntent;
            _network.PeerPathStatusReceived -= HandlePeerPathStatus;
            _network.AuthoritativePathFinalReceived -= HandleAuthoritativePathFinal;
            _network.PlayerDisconnectedTemporary -= HandleDisconnectTemporary;
            _network.PlayerDisconnectedPermanent -= HandleDisconnectPermanent;
            _network.ConnectionStateChanged -= HandleConnectionStateChanged;
        }

        private void OnDestroy()
        {
            UnhookNetworkEvents();
        }

        private void PopulateCards()
        {
            if (_config == null || _config.availablePaths == null) return;

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                if (i < _config.availablePaths.Count && _config.availablePaths[i] != null)
                {
                    var path = _config.availablePaths[i];
                    if (_pathNames[i] != null) _pathNames[i].text = path.displayName.ToUpper();
                    if (_pathDescriptions[i] != null) _pathDescriptions[i].text = path.description;

                    if (_pathStats[i] != null)
                    {
                        string statLine = "";
                        if (path.payload.maxHpModifier != 0)
                            statLine += $"HP: {(path.payload.maxHpModifier > 0 ? "+" : "")}{path.payload.maxHpModifier}  ";
                        if (path.payload.attackMultiplierBonus > 0f)
                            statLine += $"ATK: +{path.payload.attackMultiplierBonus * 100f:0}%  ";
                        if (path.payload.damageReductionModifier > 0f)
                            statLine += $"DEF: +{path.payload.damageReductionModifier * 100f:0}% Reduction  ";
                        if (path.payload.bonusManaRegenPerTurn > 0)
                            statLine += $"MANA REGEN: +{path.payload.bonusManaRegenPerTurn}/turn";

                        _pathStats[i].text = !string.IsNullOrEmpty(statLine) ? statLine : "Balanced Vitals";
                    }

                    if (_pathCards[i] != null)
                    {
                        _pathCards[i].onClick.RemoveAllListeners();
                        _pathCards[i].onClick.AddListener(() => OnCardClicked(index));
                    }
                }
            }

            if (_btnLockIn != null)
            {
                _btnLockIn.onClick.RemoveAllListeners();
                _btnLockIn.onClick.AddListener(OnLockInClicked);
            }
        }

        public void OnCardClicked(int index)
        {
            if (_isLocalLockedIn) return; // Prevent changing after lock-in

            _selectedPathIndex = index;
            HighlightCard(index);
        }

        private void HighlightCard(int selectedIndex)
        {
            for (int i = 0; i < 3; i++)
            {
                if (_pathHighlights[i] != null)
                {
                    _pathHighlights[i].color = (i == selectedIndex)
                        ? new Color(1.0f, 0.85f, 0.2f, 0.9f)
                        : new Color(0.2f, 0.2f, 0.2f, 0.4f);
                }
            }
        }

        public void OnLockInClicked()
        {
            if (_isLocalLockedIn || _selectedPathIndex < 0 || _config == null) return;

            _isLocalLockedIn = true;
            if (_btnLockIn != null) _btnLockIn.interactable = false;
            if (_txtLockIn != null) _txtLockIn.text = "LOCKED IN";

            for (int i = 0; i < 3; i++)
            {
                if (_pathCards[i] != null) _pathCards[i].interactable = false;
            }

            PathDefinition chosen = GetSelectedPath();
            string chosenId = chosen != null ? chosen.pathId : "BALANCED";

            if (_txtStatusBanner != null)
                _txtStatusBanner.text = $"PATH CONFIRMED: {chosen?.displayName.ToUpper()}. Waiting for opponent...";

            if (_isHost)
            {
                _hostChosenPathId = chosenId;
                _network?.BroadcastPathStatus(_localPlayerId, true);
                CheckHostResolution();
            }
            else
            {
                _network?.SendPathIntent(_localPlayerId, chosenId);
            }
        }

        private PathDefinition GetSelectedPath()
        {
            if (_config == null || _config.availablePaths == null || _config.availablePaths.Count == 0)
                return null;

            if (_selectedPathIndex >= 0 && _selectedPathIndex < _config.availablePaths.Count)
                return _config.availablePaths[_selectedPathIndex];

            return _config.defaultFallbackPath ?? _config.availablePaths[0];
        }

        private IEnumerator CountdownRoutine()
        {
            float totalTime = _timeRemaining;
            while (_timeRemaining > 0f)
            {
                if (!_isPausedForReconnect)
                {
                    _timeRemaining -= Time.deltaTime;
                    if (_txtTimer != null)
                        _txtTimer.text = $"{Mathf.CeilToInt(Mathf.Max(0f, _timeRemaining))}s";

                    if (_sliderTimer != null && totalTime > 0f)
                        _sliderTimer.value = Mathf.Clamp01(_timeRemaining / totalTime);
                }
                yield return null;
            }

            // Timeout Expired: Auto-assign default path if not locked in
            if (!_isLocalLockedIn)
            {
                Debug.Log("[PATH SELECTION] Countdown expired! Auto-assigning default path.");
                if (_selectedPathIndex < 0)
                {
                    _selectedPathIndex = _config.availablePaths.FindIndex(p => p.archetype == PathArchetype.Balanced);
                    if (_selectedPathIndex < 0) _selectedPathIndex = 0;
                }
                OnLockInClicked();
            }

            if (_isHost)
            {
                // Force authoritative resolution on timeout
                HostForceResolutionOnTimeout();
            }
        }

        private void HandleRemotePathIntent(string playerId, string pathId)
        {
            if (!_isHost) return;

            _guestChosenPathId = pathId;
            _isOpponentLockedIn = true;
            if (_txtOpponentStatus != null) _txtOpponentStatus.text = "Opponent locked in!";

            _network?.BroadcastPathStatus(playerId, true);
            CheckHostResolution();
        }

        private void HandlePeerPathStatus(string playerId, bool isLockedIn)
        {
            if (isLockedIn)
            {
                _isOpponentLockedIn = true;
                if (_txtOpponentStatus != null) _txtOpponentStatus.text = "Opponent locked in!";
            }
        }

        private void CheckHostResolution()
        {
            if (!_isHost) return;

            // In Single-player / CPU match mode, auto-pick for opponent if needed
            if (_coordinator != null && _coordinator.IsCpuOpponentEnabled && string.IsNullOrEmpty(_guestChosenPathId))
            {
                _guestChosenPathId = "DEFENSIVE";
                _isOpponentLockedIn = true;
                if (_txtOpponentStatus != null) _txtOpponentStatus.text = "Opponent locked in!";
            }

            if (!string.IsNullOrEmpty(_hostChosenPathId) && !string.IsNullOrEmpty(_guestChosenPathId))
            {
                FinalizeHostSync(_hostChosenPathId, _guestChosenPathId);
            }
        }

        private void HostForceResolutionOnTimeout()
        {
            string fallbackId = _config != null && _config.defaultFallbackPath != null
                ? _config.defaultFallbackPath.pathId
                : "BALANCED";

            if (string.IsNullOrEmpty(_hostChosenPathId)) _hostChosenPathId = fallbackId;
            if (string.IsNullOrEmpty(_guestChosenPathId)) _guestChosenPathId = fallbackId;

            FinalizeHostSync(_hostChosenPathId, _guestChosenPathId);
        }

        private void FinalizeHostSync(string hostPathId, string guestPathId)
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            // Authoritative Broadcast to all clients
            _network?.BroadcastPathFinal(hostPathId, guestPathId);

            // Execute locally for host
            CompletePathSelection(hostPathId, guestPathId);
        }

        private void HandleAuthoritativePathFinal(string hostPathId, string guestPathId)
        {
            if (_isHost) return; // Host already resolved

            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            CompletePathSelection(hostPathId, guestPathId);
        }

        private void CompletePathSelection(string hostPathId, string guestPathId)
        {
            IsSelectionActive = false;
            UnhookNetworkEvents();

            PathDefinition hostPath = _config.GetPathById(hostPathId);
            PathDefinition guestPath = _config.GetPathById(guestPathId);

            PathDefinition localPath = _isHost ? hostPath : guestPath;
            PathDefinition opponentPath = _isHost ? guestPath : hostPath;

            Debug.Log($"[PATH SELECTION COMPLETE] Local: {localPath?.displayName}, Opponent: {opponentPath?.displayName}");

            if (_panelRoot != null) _panelRoot.SetActive(false);

            _onSelectionCompleted?.Invoke(localPath, opponentPath);
        }

        private void HandleDisconnectTemporary(string reason)
        {
            _isPausedForReconnect = true;
            if (_txtStatusBanner != null)
                _txtStatusBanner.text = "Opponent connection interrupted. Reconnecting (15s grace)...";
        }

        private void HandleConnectionStateChanged(NetworkConnectionState state)
        {
            if (state == NetworkConnectionState.Connected && _isPausedForReconnect)
            {
                _isPausedForReconnect = false;
                if (_txtStatusBanner != null)
                    _txtStatusBanner.text = _isLocalLockedIn ? "Waiting for opponent..." : "CHOOSE YOUR PATH";
            }
        }

        private void HandleDisconnectPermanent(string reason)
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            _isPausedForReconnect = false;
            IsSelectionActive = false;
            UnhookNetworkEvents();

            if (_txtStatusBanner != null)
                _txtStatusBanner.text = "Opponent disconnected permanently. Duel ended.";
            if (_btnLockIn != null)
            {
                _btnLockIn.interactable = true;
                _btnLockIn.onClick.RemoveAllListeners();
                _btnLockIn.onClick.AddListener(() =>
                {
                    if (_panelRoot != null) _panelRoot.SetActive(false);
                    var menu = FindAnyObjectByType<MainMenuController>();
                    menu?.ReturnToMainMenu();
                });
                if (_txtLockIn != null) _txtLockIn.text = "RETURN TO MENU";
            }
        }

        private void EnsureUIReferences()
        {
            if (_panelRoot == null)
            {
                _panelRoot = gameObject;
            }

            // Create runtime fallback UI if not wired in Inspector
            if (_btnLockIn == null)
            {
                BuildRuntimeFallbackUI();
            }
        }

        private void BuildRuntimeFallbackUI()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("PathSelectionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                transform.SetParent(canvas.transform, false);
            }

            var rootObj = new GameObject("PathSelectionPanel", typeof(RectTransform), typeof(Image));
            rootObj.transform.SetParent(transform, false);
            _panelRoot = rootObj;
            var rootRect = rootObj.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            rootObj.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.95f);

            // Title
            var titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(rootObj.transform, false);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.88f);
            titleRect.anchorMax = new Vector2(0.9f, 0.98f);
            titleRect.sizeDelta = Vector2.zero;
            _txtTitle = titleObj.GetComponent<TextMeshProUGUI>();
            _txtTitle.text = "CHOOSE YOUR COMBAT PATH";
            _txtTitle.fontSize = 38;
            _txtTitle.alignment = TextAlignmentOptions.Center;
            _txtTitle.color = Color.white;

            // Timer & Status
            var timerObj = new GameObject("TimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            timerObj.transform.SetParent(rootObj.transform, false);
            var timerRect = timerObj.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.4f, 0.82f);
            timerRect.anchorMax = new Vector2(0.6f, 0.88f);
            timerRect.sizeDelta = Vector2.zero;
            _txtTimer = timerObj.GetComponent<TextMeshProUGUI>();
            _txtTimer.text = "15s";
            _txtTimer.fontSize = 32;
            _txtTimer.alignment = TextAlignmentOptions.Center;
            _txtTimer.color = new Color(1f, 0.85f, 0.2f);

            var bannerObj = new GameObject("BannerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            bannerObj.transform.SetParent(rootObj.transform, false);
            var bannerRect = bannerObj.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.1f, 0.76f);
            bannerRect.anchorMax = new Vector2(0.9f, 0.82f);
            bannerRect.sizeDelta = Vector2.zero;
            _txtStatusBanner = bannerObj.GetComponent<TextMeshProUGUI>();
            _txtStatusBanner.text = "Select a tactical path before battle loads";
            _txtStatusBanner.fontSize = 20;
            _txtStatusBanner.alignment = TextAlignmentOptions.Center;
            _txtStatusBanner.color = new Color(0.8f, 0.8f, 0.8f);

            // 3 Path Cards Container
            for (int i = 0; i < 3; i++)
            {
                float xMin = 0.08f + i * 0.30f;
                float xMax = xMin + 0.25f;

                var cardObj = new GameObject($"PathCard_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardObj.transform.SetParent(rootObj.transform, false);
                var cardRect = cardObj.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(xMin, 0.26f);
                cardRect.anchorMax = new Vector2(xMax, 0.74f);
                cardRect.sizeDelta = Vector2.zero;
                _pathCards[i] = cardObj.GetComponent<Button>();

                var outlineObj = new GameObject("Outline", typeof(RectTransform), typeof(Image));
                outlineObj.transform.SetParent(cardObj.transform, false);
                var outlineRect = outlineObj.GetComponent<RectTransform>();
                outlineRect.anchorMin = Vector2.zero;
                outlineRect.anchorMax = Vector2.one;
                outlineRect.sizeDelta = new Vector2(6f, 6f);
                _pathHighlights[i] = outlineObj.GetComponent<Image>();
                _pathHighlights[i].color = new Color(0.2f, 0.2f, 0.2f, 0.4f);
                cardObj.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 1f);

                // Name
                var nameObj = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
                nameObj.transform.SetParent(cardObj.transform, false);
                var nameRect = nameObj.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0.05f, 0.82f);
                nameRect.anchorMax = new Vector2(0.95f, 0.95f);
                nameRect.sizeDelta = Vector2.zero;
                _pathNames[i] = nameObj.GetComponent<TextMeshProUGUI>();
                _pathNames[i].fontSize = 24;
                _pathNames[i].alignment = TextAlignmentOptions.Center;
                _pathNames[i].color = Color.white;

                // Description
                var descObj = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
                descObj.transform.SetParent(cardObj.transform, false);
                var descRect = descObj.GetComponent<RectTransform>();
                descRect.anchorMin = new Vector2(0.08f, 0.35f);
                descRect.anchorMax = new Vector2(0.92f, 0.80f);
                descRect.sizeDelta = Vector2.zero;
                _pathDescriptions[i] = descObj.GetComponent<TextMeshProUGUI>();
                _pathDescriptions[i].fontSize = 17;
                _pathDescriptions[i].alignment = TextAlignmentOptions.TopLeft;
                _pathDescriptions[i].color = new Color(0.85f, 0.85f, 0.85f);

                // Stat Box
                var statObj = new GameObject("Stats", typeof(RectTransform), typeof(TextMeshProUGUI));
                statObj.transform.SetParent(cardObj.transform, false);
                var statRect = statObj.GetComponent<RectTransform>();
                statRect.anchorMin = new Vector2(0.08f, 0.05f);
                statRect.anchorMax = new Vector2(0.92f, 0.32f);
                statRect.sizeDelta = Vector2.zero;
                _pathStats[i] = statObj.GetComponent<TextMeshProUGUI>();
                _pathStats[i].fontSize = 16;
                _pathStats[i].alignment = TextAlignmentOptions.Center;
                _pathStats[i].color = new Color(0.3f, 0.9f, 0.5f);
            }

            // Opponent Status
            var oppObj = new GameObject("OpponentStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
            oppObj.transform.SetParent(rootObj.transform, false);
            var oppRect = oppObj.GetComponent<RectTransform>();
            oppRect.anchorMin = new Vector2(0.2f, 0.18f);
            oppRect.anchorMax = new Vector2(0.8f, 0.24f);
            oppRect.sizeDelta = Vector2.zero;
            _txtOpponentStatus = oppObj.GetComponent<TextMeshProUGUI>();
            _txtOpponentStatus.text = "Opponent is choosing...";
            _txtOpponentStatus.fontSize = 20;
            _txtOpponentStatus.alignment = TextAlignmentOptions.Center;
            _txtOpponentStatus.color = new Color(0.6f, 0.8f, 1f);

            // Lock In Button
            var btnObj = new GameObject("BtnLockIn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(rootObj.transform, false);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.35f, 0.06f);
            btnRect.anchorMax = new Vector2(0.65f, 0.15f);
            btnRect.sizeDelta = Vector2.zero;
            btnObj.GetComponent<Image>().color = new Color(0.85f, 0.45f, 0.1f);
            _btnLockIn = btnObj.GetComponent<Button>();

            var btnTxtObj = new GameObject("BtnText", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTxtObj.transform.SetParent(btnObj.transform, false);
            var btnTxtRect = btnTxtObj.GetComponent<RectTransform>();
            btnTxtRect.anchorMin = Vector2.zero;
            btnTxtRect.anchorMax = Vector2.one;
            btnTxtRect.sizeDelta = Vector2.zero;
            _txtLockIn = btnTxtObj.GetComponent<TextMeshProUGUI>();
            _txtLockIn.text = "LOCK IN PATH";
            _txtLockIn.fontSize = 24;
            _txtLockIn.alignment = TextAlignmentOptions.Center;
            _txtLockIn.color = Color.white;

            rootObj.SetActive(false);
        }
    }
}
