using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using ArGuildEncounter.Battle;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Spells;
using ArGuildEncounter.AR;
using ArGuildEncounter.DebugSuite;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Networking;

namespace ArGuildEncounter.UI
{
    /// <summary>
    /// Top-level application and screen states per project implementation plan.
    /// </summary>
    public enum ScreenState
    {
        MainMenu = 0,
        Matchmaking = 1,
        WaitingRoom = 2,
        CombatHUD = 3,
        GuildSelection = 4,
        BattleConclusion = 5,
        PathSelection = 6
    }

    /// <summary>
    /// Mobile-first Battle HUD Controller and Visual Debug Presentation.
    /// Presents player/opponent vitals, authoritative cast countdown,
    /// targeting reticle status, 6 spell slots, action instruction banner, combat log,
    /// and interactive debug mock controls.
    /// </summary>
    public class BattleHUDController : MonoBehaviour
    {
        [Header("Local Player Vitals (Top-Left)")]
        public Text playerNameText;
        public Text playerGuildText;
        public Slider playerHpSlider;
        public Text playerHpText;
        public Slider playerManaSlider;
        public Text playerManaText;

        [Header("Opponent Vitals (Top-Right)")]
        public Text opponentNameText;
        public Text opponentGuildText;
        public Slider opponentHpSlider;
        public Text opponentHpText;
        public Slider opponentManaSlider;
        public Text opponentManaText;

        [Header("Turn & Match Status (Top-Center)")]
        public Text turnIndicatorText;
        public Text turnNumberText;
        public Text battlePhaseText;

        [Header("Instruction Banner & Resolution (Center-Bottom)")]
        public Text instructionBannerText;
        public Text combatResultText;

        [Header("Cast Timer & Spell Focus")]
        public Text selectedSpellText;
        public Slider castTimerSlider;
        public Text castTimerText;

        [Header("AR Target Reticle (Center)")]
        public Image targetingReticle;
        public Text targetStatusText;

        [Header("6 Spell Slot Buttons (Bottom)")]
        public const int SpellSlotCount = 6;
        public Button[] spellSlotButtons = new Button[SpellSlotCount];
        public Text[] spellSlotTexts = new Text[SpellSlotCount];

        [Header("Status Effect Overlays")]
        public Text playerStatusText;
        public Text opponentStatusText;

        [Header("Combat Log & Messages")]
        public Text combatLogText;

        [Header("Combat Quick Action Button")]
        public Button btnActionButton;
        public Text btnActionButtonText;

        [Header("State-Aware Debug Controls (16 Dev Actions)")]
        public GameFoundationRunner runner;
        public Button btnVoice;
        public Text btnVoiceText;
        public Button btnVoiceFail;
        public Button btnTapSlot1;
        public Button btnGestureSuccess;
        public Text btnGestureSuccessText;
        public Button btnGestureFail;
        public Button btnGestureTimeout;
        public Button btnAimLock;
        public Button btnAimLost;
        public Button btnLaunch;
        public Button btnForceMiss;
        public Text btnForceMissText;
        public Button btnForceCrit;
        public Text btnForceCritText;
        public Button btnDisconnect;
        public Button btnReconnect;
        public Button btnTeleport;
        public Button btnQuickStart;
        public Button btnResetBattle;
        [Header("15 Authoritative Developer Controls")]
        public Button btnCreateRoom;
        public Button btnJoinRoom;
        public Button btnStartBattle;
        public Button btnApplyStatus;
        public Button btnToggleCpu;
        public Text btnToggleCpuText;
        public Button btnToggleDiagnostics;

        [Header("Guild Selection UI")]
        public GameObject panelGuildSelection;
        public Text textSelectedGuildTitle;
        public Text textSelectedGuildAffinity;
        public Text textSelectedGuildPassive;
        public Text textSelectedGuildPlaystyle;
        public Button btnConfirmGuild;
        private GuildData _selectedGuild;

        [Header("Battle Conclusion UI")]
        public GameObject panelBattleConclusion;
        public Text textConclusionTitle;
        public Text textConclusionWinner;
        public Text textConclusionDetails;
        public Button btnConclusionRestart;

        [Header("Combat Diagnostic Panel")]
        public GameObject panelDiagnostics;
        public Text diagHpTraceText;
        public Text diagManaTraceText;
        public Text diagMissFormulaText;
        public Text diagDamageFormulaText;
        public Text diagModifiersText;
        public Text diagStatusesText;
        public Text diagHandTrackingText;

        [Header("Live Camera Feed & Hand Tracking Viewport")]
        public GameObject panelCameraFeed;
        public RawImage cameraFeedRawImage;
        public Image cameraHandReticle;
        public Text textCameraStatus;

        [Header("Visual Attack Presentation")]
        public Transform vfxContainer;
        public Text floatingDamageText;
        private Coroutine _activeAttackVfxCoroutine;
        private Coroutine _localHpLerpCoroutine;
        private Coroutine _opponentHpLerpCoroutine;

        [Header("Explicit Gesture & Spell Debug Readouts")]
        public Text dbgCurrentSpellText;
        public Text dbgExpectedGestureText;
        public Text dbgGestureConfidenceText;

        [Header("Matchmaking & 6-Digit Room UI")]
        public GameObject panelMatchmaking;
        public Text textRoomCodeDisplay;
        public InputField inputRoomCode;
        public InputField inputManualIp;
        public Button btnHostCreate;
        public Button btnClientJoin;

        [Header("Top-Level Screens & Waiting Room")]
        public GameObject panelMainMenu;
        public GameObject panelWaitingRoom;
        public GameObject panelPathSelection;
        public PathSelectionUIController pathSelectionUIController;
        public Text textWaitingRoomCode;
        public Text textWaitingStatus;
        public Button btnWaitingStartDuel;
        public Button btnWaitingLeave;
        public Button btnConclusionMainMenu;
        public Text textPvPStatus;
        public ScreenState CurrentScreen { get; private set; } = ScreenState.MainMenu;

        [Header("Procedural Panel References")]
        [SerializeField] private GameObject _playerPanel;
        [SerializeField] private GameObject _oppPanel;
        [SerializeField] private GameObject _statusPanel;
        [SerializeField] private GameObject _centerPanel;
        [SerializeField] private GameObject _bannerPanel;
        [SerializeField] private GameObject _spellPanel;
        [SerializeField] private GameObject _logPanel;
        [SerializeField] private GameObject _debugPanel;

        [Header("Shared Reference Calibration UI")]
        public GameObject panelCalibration;
        public Text textCalibrationInstruction;
        public Button btnConfirmOrigin;
        public Button btnDevManualOriginFallback;

        [Header("14-Metric Development Debug Overlay")]
        public GameObject panelDevMetrics;
        public Text textDevMetrics;
        public Button btnToggleDevMetrics;

        private BattleCoordinator _coordinator;
        private bool _localEventsHooked = false;
        private bool _opponentEventsHooked = false;

        private void Awake()
        {
            EnsureVisualHUD();
        }

        private void Update()
        {
            if (_coordinator != null)
            {
                UpdateDebugButtonsState(_coordinator.StateMachine.CurrentPhase);
                UpdateDevMetricsOverlay();

                // Section 13 Developer Diagnostics for Hand Tracking
                if (diagHandTrackingText != null && panelDiagnostics != null && panelDiagnostics.activeSelf)
                {
                    var ht = _coordinator.GestureInput as ArGuildEncounter.Gesture.HandTrackingGestureService;
                    if (ht != null)
                    {
                        string trackingStr = ht.IsTrackingActive ? "ON" : "OFF";
                        string handsUpStr = ht.IsHandsUpDetected ? "YES" : "NO";
                        string gestureStr = ht.CurrentGesture;
                        string resultStr = ht.ResultState.ToString().ToUpper();
                        float timerVal = (_coordinator.StateMachine.CurrentPhase == BattlePhase.GestureWindow)
                            ? _coordinator.CastTimeRemaining
                            : ht.TimeRemaining;

                        diagHandTrackingText.text = $"[HAND TRACKING DIAGNOSTICS]\n" +
                            $"Hand Tracking: {trackingStr}\n" +
                            $"Hands Detected: {ht.HandsDetectedCount}\n" +
                            $"Hands Up: {handsUpStr}\n" +
                            $"Confidence: {ht.CurrentConfidence * 100f:0.0}%\n" +
                            $"Gesture: {gestureStr}\n" +
                            $"Gesture Timer: {timerVal:F1} s\n" +
                            $"Gesture Result: {resultStr}";
                    }
                    else
                    {
                        bool isTracking = _coordinator.GestureInput?.IsTrackingGesture == true;
                        float timerVal = _coordinator.StateMachine.CurrentPhase == BattlePhase.GestureWindow ? _coordinator.CastTimeRemaining : 0f;
                        diagHandTrackingText.text = $"[HAND TRACKING DIAGNOSTICS]\n" +
                            $"Hand Tracking: {(isTracking ? "ON" : "OFF")}\n" +
                            $"Hands Detected: 0\n" +
                            $"Hands Up: NO\n" +
                            $"Confidence: 0.0%\n" +
                            $"Gesture: NONE\n" +
                            $"Gesture Timer: {timerVal:F1} s\n" +
                            $"Gesture Result: WAITING";
                    }
                }

                // Live Camera Viewport Presentation during GestureWindow
                if (panelCameraFeed != null)
                {
                    bool isGestureWindow = _coordinator != null && _coordinator.StateMachine.CurrentPhase == BattlePhase.GestureWindow;
                    panelCameraFeed.SetActive(isGestureWindow);

                    if (isGestureWindow)
                    {
                        var ht = _coordinator.GestureInput as ArGuildEncounter.Gesture.HandTrackingGestureService;
                        if (ht != null)
                        {
                            if (cameraFeedRawImage != null && ht.WebcamTexture != null)
                            {
                                if (cameraFeedRawImage.texture != ht.WebcamTexture)
                                {
                                    cameraFeedRawImage.texture = ht.WebcamTexture;
                                }
                            }

                            if (cameraHandReticle != null)
                            {
                                if (ht.HandsDetectedCount > 0)
                                {
                                    cameraHandReticle.gameObject.SetActive(true);
                                    var vpRect = cameraFeedRawImage != null ? cameraFeedRawImage.rectTransform : null;
                                    if (vpRect != null)
                                    {
                                        // Mirror X to match horizontally flipped camera selfie preview
                                        float rx = (0.5f - ht.LastTrackedPalmCenter.x) * vpRect.rect.width;
                                        float ry = (ht.LastTrackedPalmCenter.y - 0.5f) * vpRect.rect.height;
                                        cameraHandReticle.rectTransform.anchoredPosition = new Vector2(rx, ry);
                                    }
                                    cameraHandReticle.color = ht.IsHandsUpDetected
                                        ? new Color(0.2f, 1f, 0.4f, 0.95f)
                                        : new Color(0.2f, 0.8f, 1f, 0.75f);
                                }
                                else
                                {
                                    cameraHandReticle.gameObject.SetActive(false);
                                }
                            }

                            if (textCameraStatus != null)
                            {
                                if (ht.IsWebcamActive)
                                {
                                    string handState = ht.IsHandsUpDetected ? "<color=#55FF55>HANDS UP DETECTED</color>" : (ht.HandsDetectedCount > 0 ? "TRACKING HAND" : "SEARCHING FOR HANDS");
                                    textCameraStatus.text = $"CAMERA: LIVE | {handState} ({ht.CurrentConfidence * 100f:0}%)";
                                }
                                else
                                {
                                    textCameraStatus.text = "CAMERA: INITIALIZING / STANDBY [Press 'U' for Hands-Up]";
                                }
                            }
                        }
                    }
                }

                // Keyboard shortcuts for interactive gameplay / testing (migrated to UnityEngine.InputSystem)
                if (_coordinator.StateMachine.IsInBattleLoop)
                {
                    var keyboard = Keyboard.current;
                    if (keyboard != null)
                    {
                        Key[] digitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
                        Key[] numpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6 };

                        for (int i = 0; i < 6; i++)
                        {
                            if (keyboard[digitKeys[i]].wasPressedThisFrame || keyboard[numpadKeys[i]].wasPressedThisFrame)
                            {
                                _coordinator.SelectSpellManualFallback(i);
                                AppendLog($"[HOTKEY] Spell Slot {i + 1} chosen via key '{i + 1}'.");
                            }
                        }

                        if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                        {
                            var phase = _coordinator.StateMachine.CurrentPhase;
                            if (phase == BattlePhase.AimPhase)
                            {
                                runner?.TestAttackLaunch();
                                AppendLog("[HOTKEY] Attack launched via Space/Enter!");
                            }
                            else if (phase == BattlePhase.GestureWindow)
                            {
                                runner?.TestHandsUp();
                                AppendLog("[HOTKEY] Hands-Up (GESTURE_THRUST) confirmed via Space/Enter!");
                            }
                            else if (phase == BattlePhase.SpellSelection || phase == BattlePhase.PlayerTurn)
                            {
                                runner?.SimulateVoiceForActivePlayer();
                            }
                        }
                    }
                }
            }
        }

        public void BindCoordinator(BattleCoordinator coordinator)
        {
            _coordinator = coordinator;
            if (_coordinator == null) return;

            _coordinator.OnMatchReset += HandleMatchReset;
            _coordinator.OnMatchSetupCompleted += RefreshInitialUI;
            _coordinator.StateMachine.PhaseChanged += OnPhaseChanged;
            _coordinator.OnBattleLog += AppendLog;
            _coordinator.OnCastTimerUpdated += UpdateCastTimer;
            _coordinator.OnAttackResolved += HandleAttackResolved;
            _coordinator.OnSpellSelectionRejected += HandleSpellSelectionRejected;

            if (_coordinator.ARTracking != null)
            {
                _coordinator.ARTracking.OnTargetStatusUpdated += UpdateTargetReticle;
            }

            if (_coordinator.NetworkSession != null)
            {
                _coordinator.NetworkSession.PlayerConnected += HandleNetworkPlayerConnected;
                _coordinator.NetworkSession.PlayerDisconnectedPermanent += HandleNetworkPlayerDisconnected;
            }

            RefreshInitialUI();
        }

        private void HandleNetworkPlayerConnected(string guestId)
        {
            if (textWaitingStatus != null)
            {
                textWaitingStatus.text = $"Player 2 ({guestId}) connected! Ready to duel.";
            }
            if (btnWaitingStartDuel != null)
            {
                btnWaitingStartDuel.interactable = true;
            }
        }

        private void HandleNetworkPlayerDisconnected(string playerId)
        {
            if (textWaitingStatus != null)
            {
                textWaitingStatus.text = $"Opponent ({playerId}) disconnected.";
            }
        }

        private void HandleMatchReset()
        {
            if (combatLogText != null)
            {
                combatLogText.text = "Duel reset to WaitingForPlayer.\nReady for combat.";
            }
            if (combatResultText != null)
            {
                combatResultText.text = "Awaiting duel initiation...";
                combatResultText.color = new Color(0.8f, 0.9f, 1f);
            }
            if (selectedSpellText != null)
            {
                selectedSpellText.text = "Spell: None";
            }
            if (castTimerSlider != null) castTimerSlider.value = 0f;
            if (castTimerText != null) castTimerText.text = "0.0s";
            if (targetStatusText != null)
            {
                targetStatusText.text = "SEARCHING FOR OPPONENT...";
                targetStatusText.color = Color.yellow;
            }
            if (targetingReticle != null)
            {
                targetingReticle.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            }
            RefreshInitialUI();
        }

        public void RefreshInitialUI()
        {
            if (_coordinator == null) return;

            // Bind Local Player
            if (_coordinator.LocalPlayer != null)
            {
                if (playerNameText != null)
                {
                    playerNameText.text = $"{_coordinator.LocalPlayer.PlayerId} (YOU)";
                }
                if (playerGuildText != null && _coordinator.LocalPlayer.Guild != null)
                {
                    playerGuildText.text = $"Guild: {_coordinator.LocalPlayer.Guild.displayName}";
                }

                if (!_localEventsHooked)
                {
                    _coordinator.LocalPlayer.OnHPChanged += (cur, max) => UpdateLocalHP(cur, max);
                    _coordinator.LocalPlayer.OnManaChanged += (cur, max) => UpdateLocalMana(cur, max);
                    _localEventsHooked = true;
                }

                UpdateLocalHP(_coordinator.LocalPlayer.CurrentHP, _coordinator.LocalPlayer.MaxHP);
                UpdateLocalMana(_coordinator.LocalPlayer.CurrentMana, _coordinator.LocalPlayer.MaxMana);

                // Bind 6 spell slots
                if (spellSlotButtons == null || spellSlotButtons.Length != SpellSlotCount)
                {
                    spellSlotButtons = new Button[SpellSlotCount];
                }
                if (spellSlotTexts == null || spellSlotTexts.Length != SpellSlotCount)
                {
                    spellSlotTexts = new Text[SpellSlotCount];
                }

                for (int i = 0; i < SpellSlotCount; i++)
                {
                    int index = i;
                    var spell = (_coordinator.LocalPlayer.SpellSlots != null && i < _coordinator.LocalPlayer.SpellSlots.Length)
                        ? _coordinator.LocalPlayer.SpellSlots[i]
                        : null;

                    if (i < spellSlotTexts.Length && spellSlotTexts[i] != null)
                    {
                        if (spell != null)
                        {
                            string fallbackBadge = (i == SpellSlotCount - 1) ? "\n[Fallback]" : "";
                            spellSlotTexts[i].text = $"{spell.displayName}\n({spell.manaCost} MP){fallbackBadge}";
                        }
                        else
                        {
                            spellSlotTexts[i].text = "[Empty]";
                        }
                    }
                    if (i < spellSlotButtons.Length && spellSlotButtons[i] != null)
                    {
                        spellSlotButtons[i].onClick.RemoveAllListeners();
                        spellSlotButtons[i].onClick.AddListener(() =>
                        {
                            _coordinator.SelectSpellManualFallback(index);
                            AppendLog($"[UI] Selected Slot {index + 1}: {spell?.displayName}");
                        });
                    }
                }
            }

            // Bind Opponent Player
            if (_coordinator.OpponentPlayer != null)
            {
                if (opponentNameText != null)
                {
                    opponentNameText.text = $"{_coordinator.OpponentPlayer.PlayerId} (OPPONENT)";
                }
                if (opponentGuildText != null && _coordinator.OpponentPlayer.Guild != null)
                {
                    opponentGuildText.text = $"Guild: {_coordinator.OpponentPlayer.Guild.displayName}";
                }

                if (!_opponentEventsHooked)
                {
                    _coordinator.OpponentPlayer.OnHPChanged += (cur, max) => UpdateOpponentHP(cur, max);
                    _coordinator.OpponentPlayer.OnManaChanged += (cur, max) => UpdateOpponentMana(cur, max);
                    _opponentEventsHooked = true;
                }

                UpdateOpponentHP(_coordinator.OpponentPlayer.CurrentHP, _coordinator.OpponentPlayer.MaxHP);
                UpdateOpponentMana(_coordinator.OpponentPlayer.CurrentMana, _coordinator.OpponentPlayer.MaxMana);
            }

            UpdateTurnIndicator();
        }

        private void OnPhaseChanged(object sender, BattlePhaseChangedEventArgs e)
        {
            if (battlePhaseText != null)
            {
                battlePhaseText.text = $"PHASE: {e.CurrentPhase}";
            }

            if (_coordinator?.TurnManager != null && turnNumberText != null)
            {
                turnNumberText.text = $"TURN: {_coordinator.TurnManager.CurrentTurnNumber}";
            }

            // Re-check players if initialized after bind
            if ((!_localEventsHooked && _coordinator?.LocalPlayer != null) ||
                (!_opponentEventsHooked && _coordinator?.OpponentPlayer != null))
            {
                RefreshInitialUI();
            }

            if (_coordinator?.SelectedSpell != null && selectedSpellText != null)
            {
                selectedSpellText.text = $"Selected: {_coordinator.SelectedSpell.displayName} ({_coordinator.SelectedSpell.tier})";
            }
            else if (selectedSpellText != null)
            {
                selectedSpellText.text = "Spell: None";
            }

            if (panelGuildSelection != null)
            {
                panelGuildSelection.SetActive(e.CurrentPhase == BattlePhase.GuildSelection);
                if (e.CurrentPhase == BattlePhase.GuildSelection)
                {
                    CurrentScreen = ScreenState.GuildSelection;
                }
            }

            if (panelPathSelection != null)
            {
                panelPathSelection.SetActive(e.CurrentPhase == BattlePhase.PathSelection);
                if (e.CurrentPhase == BattlePhase.PathSelection)
                {
                    CurrentScreen = ScreenState.PathSelection;
                }
            }

            if (panelCalibration != null)
            {
                panelCalibration.SetActive(e.CurrentPhase == BattlePhase.ARCalibration);
            }

            if (panelBattleConclusion != null)
            {
                bool isBattleEnd = e.CurrentPhase == BattlePhase.BattleEnd;
                panelBattleConclusion.SetActive(isBattleEnd);
                if (isBattleEnd)
                {
                    CurrentScreen = ScreenState.BattleConclusion;
                    UpdateBattleConclusionDisplay();
                }
            }

            if (e.CurrentPhase == BattlePhase.BattleReady || e.CurrentPhase == BattlePhase.PlayerTurn)
            {
                if (CurrentScreen != ScreenState.CombatHUD && CurrentScreen != ScreenState.GuildSelection && CurrentScreen != ScreenState.BattleConclusion && CurrentScreen != ScreenState.PathSelection)
                {
                    ShowScreen(ScreenState.CombatHUD);
                }
            }

            UpdateInstructionBanner(e.CurrentPhase);
            UpdateTurnIndicator();

            // Update Quick Action Button dynamically
            if (btnActionButton != null && btnActionButtonText != null)
            {
                bool isMyTurn = _coordinator != null && _coordinator.TurnManager != null && _coordinator.TurnManager.ActivePlayerId == _coordinator.LocalPlayer?.PlayerId;
                if (e.CurrentPhase == BattlePhase.AimPhase && isMyTurn)
                {
                    btnActionButton.gameObject.SetActive(true);
                    btnActionButtonText.text = "🎯 TARGET LOCKED! LAUNCHING ATTACK! ⚡";
                    var colors = btnActionButton.colors;
                    colors.normalColor = new Color(0.15f, 0.75f, 0.35f);
                    btnActionButton.colors = colors;
                }
                else if (e.CurrentPhase == BattlePhase.GestureWindow && isMyTurn)
                {
                    btnActionButton.gameObject.SetActive(true);
                    btnActionButtonText.text = "✋ RAISE HANDS UP (OR TAP TO SIMULATE) ✋";
                    var colors = btnActionButton.colors;
                    colors.normalColor = new Color(0.18f, 0.55f, 0.75f);
                    btnActionButton.colors = colors;
                }
                else if ((e.CurrentPhase == BattlePhase.SpellSelection || e.CurrentPhase == BattlePhase.PlayerTurn) && isMyTurn)
                {
                    btnActionButton.gameObject.SetActive(true);
                    btnActionButtonText.text = "🗣️ SAY SPELL NAME (OR TAP 1-6 BELOW)";
                    var colors = btnActionButton.colors;
                    colors.normalColor = new Color(0.2f, 0.35f, 0.6f);
                    btnActionButton.colors = colors;
                }
                else if (e.CurrentPhase == BattlePhase.AttackResolution || e.CurrentPhase == BattlePhase.AttackLaunched || e.CurrentPhase == BattlePhase.HPCheck || e.CurrentPhase == BattlePhase.NextTurn)
                {
                    btnActionButton.gameObject.SetActive(false);
                }
            }
        }

        private void HandleAttackResolved(AttackResolutionResult result)
        {
            if (combatResultText != null)
            {
                if (!result.isHit)
                {
                    combatResultText.text = $"ATTACK MISSED! (Miss Chance: {result.finalMissChance * 100f:0.0}%)";
                    combatResultText.color = new Color(1f, 0.4f, 0.4f);
                }
                else
                {
                    string critStr = result.isCritical ? " [CRITICAL HIT!]" : "";
                    string selfDmgStr = result.didSelfDamageTrigger ? $" (Backfire: {result.selfDamageDealt} self HP)" : "";
                    combatResultText.text = $"Attack dealt {result.finalDamageDealt} DMG{critStr}{selfDmgStr}! {result.summaryText}";
                    combatResultText.color = result.isCritical ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.6f);
                }
            }

            if (result.diagnosticData != null)
            {
                UpdateDiagnosticDisplay(result.diagnosticData);
            }

            try
            {
                if (Application.isPlaying && isActiveAndEnabled)
                {
                    if (_activeAttackVfxCoroutine != null) StopCoroutine(_activeAttackVfxCoroutine);
                    _activeAttackVfxCoroutine = StartCoroutine(PlayAttackPresentationCoroutine(result));
                }
            }
            catch { }
        }

        private void UpdateBattleConclusionDisplay()
        {
            if (_coordinator == null) return;
            var p1 = _coordinator.LocalPlayer;
            var p2 = _coordinator.OpponentPlayer;
            if (p1 == null || p2 == null) return;

            bool p1Alive = p1.IsAlive;
            bool p2Alive = p2.IsAlive;

            if (textConclusionTitle != null)
            {
                if (!p1Alive && !p2Alive)
                {
                    textConclusionTitle.text = "DRAW!";
                    textConclusionTitle.color = new Color(0.4f, 0.8f, 1f);
                }
                else if (p1Alive)
                {
                    textConclusionTitle.text = "VICTORY!";
                    textConclusionTitle.color = new Color(1f, 0.85f, 0.2f);
                }
                else
                {
                    textConclusionTitle.text = "DEFEAT!";
                    textConclusionTitle.color = new Color(1f, 0.3f, 0.3f);
                }
            }

            if (textConclusionWinner != null)
            {
                if (!p1Alive && !p2Alive)
                {
                    textConclusionWinner.text = "Both mages fell simultaneously. Honored combat.";
                }
                else if (p1Alive)
                {
                    textConclusionWinner.text = $"Winner: {p1.PlayerId} ({p1.Guild?.displayName ?? "Guild"})";
                }
                else
                {
                    textConclusionWinner.text = $"Winner: {p2.PlayerId} ({p2.Guild?.displayName ?? "Guild"})";
                }
            }

            if (textConclusionDetails != null)
            {
                int turnNum = _coordinator.TurnManager != null ? _coordinator.TurnManager.CurrentTurnNumber : 1;
                textConclusionDetails.text = $"Total Turns: {turnNum}\nYour HP: {p1.CurrentHP}/{p1.MaxHP} | Opponent HP: {p2.CurrentHP}/{p2.MaxHP}";
            }
        }

        public void UpdateDiagnosticDisplay(CombatDiagnosticData diag)
        {
            if (diag == null) return;
            if (diagHpTraceText != null) diagHpTraceText.text = $"[HP TRACE]\n{diag.hpTrace}";
            if (diagManaTraceText != null) diagManaTraceText.text = $"[MANA TRACE]\n{diag.manaTrace}";
            if (diagMissFormulaText != null) diagMissFormulaText.text = $"[MISS FORMULA]\n{diag.missTrace}";
            if (diagDamageFormulaText != null) diagDamageFormulaText.text = $"[DAMAGE FORMULA]\n{diag.damageTrace}";
            if (diagModifiersText != null) diagModifiersText.text = $"[ACTIVE MODIFIERS]\n{diag.activeModifiers}";
            if (diagStatusesText != null) diagStatusesText.text = $"[STATUSES]\nAttacker: {diag.attackerStatuses}\nDefender: {diag.defenderStatuses}";
        }

        private System.Collections.IEnumerator PlayAttackPresentationCoroutine(AttackResolutionResult result)
        {
            bool isLocalAttacking = _coordinator?.TurnManager != null && _coordinator.TurnManager.ActivePlayerId == _coordinator.LocalPlayer?.PlayerId;
            Vector3 startPos = isLocalAttacking ? new Vector3(-350f, 350f, 0f) : new Vector3(350f, 350f, 0f);
            Vector3 targetPos = targetingReticle != null ? targetingReticle.transform.localPosition : Vector3.zero;

            // 1. Spawn and animate projectile
            GameObject projectile = null;
            if (vfxContainer != null)
            {
                projectile = new GameObject("VFX_AttackProjectile", typeof(RectTransform), typeof(Image));
                projectile.transform.SetParent(vfxContainer, false);
                projectile.transform.localPosition = startPos;
                var pImg = projectile.GetComponent<Image>();
                pImg.color = result.isCritical ? new Color(1f, 0.85f, 0.2f) : (isLocalAttacking ? new Color(1f, 0.4f, 0.2f) : new Color(0.2f, 0.8f, 1f));
                var pRect = projectile.GetComponent<RectTransform>();
                pRect.sizeDelta = new Vector2(36f, 36f);

                float elapsed = 0f;
                float duration = 0.35f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    projectile.transform.localPosition = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                Destroy(projectile);
            }
            else
            {
                yield return new WaitForSeconds(0.35f);
            }

            // 2. Reticle flash
            if (targetingReticle != null)
            {
                Color origColor = targetingReticle.color;
                targetingReticle.color = result.isHit ? (result.isCritical ? Color.yellow : Color.red) : Color.gray;
                yield return new WaitForSeconds(0.12f);
                targetingReticle.color = origColor;
            }

            // 3. Floating combat text
            if (floatingDamageText != null)
            {
                if (!result.isHit)
                {
                    floatingDamageText.text = $"MISS!\n({result.finalMissChance * 100f:0.0}%)";
                    floatingDamageText.color = Color.white;
                }
                else if (result.isCritical)
                {
                    floatingDamageText.text = $"CRITICAL!\n-{result.finalDamageDealt} DMG";
                    floatingDamageText.color = new Color(1f, 0.9f, 0.2f);
                }
                else
                {
                    floatingDamageText.text = $"-{result.finalDamageDealt} DMG";
                    floatingDamageText.color = new Color(1f, 0.35f, 0.3f);
                }

                if (result.healingApplied > 0) floatingDamageText.text += $"\n+{result.healingApplied} HP";
                if (result.didSelfDamageTrigger) floatingDamageText.text += $"\n-{result.selfDamageDealt} HP (RECOIL)";
                if (result.didBackfireTrigger) floatingDamageText.text += $"\n-{result.backfireDamageDealt} HP (BLUFF)";

                floatingDamageText.gameObject.SetActive(true);
                floatingDamageText.transform.localPosition = targetPos + new Vector3(0f, 40f, 0f);

                float textElapsed = 0f;
                float textDuration = 0.55f;
                Vector3 txtStart = floatingDamageText.transform.localPosition;
                Vector3 txtEnd = txtStart + new Vector3(0f, 60f, 0f);
                while (textElapsed < textDuration)
                {
                    textElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(textElapsed / textDuration);
                    floatingDamageText.transform.localPosition = Vector3.Lerp(txtStart, txtEnd, t);
                    yield return null;
                }
                floatingDamageText.gameObject.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(0.4f);
            }

            yield return new WaitForSeconds(0.3f);

            if (_coordinator != null && !_coordinator.AutoAdvanceAfterAttack)
            {
                _coordinator.CompleteAttackAndAdvanceTurn();
            }
        }

        private void UpdateInstructionBanner(BattlePhase phase)
        {
            if (instructionBannerText == null) return;

            bool isMyTurn = _coordinator != null && _coordinator.TurnManager != null && _coordinator.TurnManager.ActivePlayerId == _coordinator.LocalPlayer?.PlayerId;

            switch (phase)
            {
                case BattlePhase.WaitingForPlayer:
                    instructionBannerText.text = "CREATING BATTLE ROOM...";
                    break;
                case BattlePhase.RoomCreated:
                    instructionBannerText.text = "ROOM CREATED: SHARE 6-DIGIT CODE";
                    break;
                case BattlePhase.PlayerJoined:
                    instructionBannerText.text = "OPPONENT CONNECTED!";
                    break;
                case BattlePhase.ARCalibration:
                    instructionBannerText.text = "CALIBRATING AR BATTLEFIELD...";
                    break;
                case BattlePhase.GuildSelection:
                    instructionBannerText.text = "CHOOSE YOUR GUILD";
                    break;
                case BattlePhase.Loadout:
                    instructionBannerText.text = "CONFIRM SPELL LOADOUT";
                    break;
                case BattlePhase.BattleReady:
                    instructionBannerText.text = "BATTLE READY!";
                    break;
                case BattlePhase.PlayerTurn:
                    instructionBannerText.text = isMyTurn ? "YOUR TURN" : "OPPONENT'S TURN";
                    break;
                case BattlePhase.SpellSelection:
                    instructionBannerText.text = isMyTurn ? "SAY SPELL NAME (OR TAP BUTTON BELOW)" : "OPPONENT IS CHOOSING SPELL...";
                    break;
                case BattlePhase.VoiceProcessing:
                    instructionBannerText.text = isMyTurn ? "LISTENING... SUMMONING SPELL" : "OPPONENT SUMMONING SPELL...";
                    break;
                case BattlePhase.SpellSelected:
                    instructionBannerText.text = isMyTurn 
                        ? $"SPELL SELECTED: {_coordinator?.SelectedSpell?.displayName?.ToUpper()}" 
                        : $"OPPONENT SELECTED: {_coordinator?.SelectedSpell?.displayName?.ToUpper()}";
                    break;
                case BattlePhase.GestureWindow:
                    instructionBannerText.text = isMyTurn 
                        ? "RAISE YOUR HANDS ✋ ✋\n[GESTURE_THRUST]" 
                        : "OPPONENT PERFORMING GESTURE: GESTURE_THRUST";
                    instructionBannerText.color = new Color(0.3f, 0.9f, 1f);
                    break;
                case BattlePhase.AimPhase:
                    instructionBannerText.text = isMyTurn 
                        ? "🎯 TARGET LOCKED!\nLAUNCHING ATTACK! ⚡" 
                        : "OPPONENT IS AIMING AT YOU...";
                    instructionBannerText.color = new Color(0.2f, 1f, 0.4f);
                    break;
                case BattlePhase.AttackLaunched:
                    instructionBannerText.text = isMyTurn 
                        ? "ATTACK LAUNCHED!" 
                        : "OPPONENT LAUNCHED ATTACK!";
                    break;
                case BattlePhase.AttackResolution:
                    instructionBannerText.text = "RESOLVING ATTACK...";
                    break;
                case BattlePhase.HPCheck:
                    instructionBannerText.text = "EVALUATING DAMAGE & HP";
                    break;
                case BattlePhase.NextTurn:
                    instructionBannerText.text = "PASSING TURN...";
                    break;
                case BattlePhase.BattleEnd:
                    instructionBannerText.text = "DUEL CONCLUDED";
                    break;
                case BattlePhase.ARTrackingLost:
                    instructionBannerText.text = "OPPONENT LOST: SEARCHING...";
                    break;
                case BattlePhase.Reconnecting:
                    instructionBannerText.text = "RECONNECTING TO BATTLE...";
                    break;
                case BattlePhase.OpponentTeleported:
                    instructionBannerText.text = "OPPONENT TELEPORTED! MATCH WON";
                    break;
                default:
                    instructionBannerText.text = phase.ToString().ToUpper();
                    break;
            }
        }

        private void UpdateTurnIndicator()
        {
            if (_coordinator?.TurnManager == null || !_coordinator.StateMachine.IsInBattleLoop)
            {
                if (turnIndicatorText != null)
                {
                    string preBattleStatus = "WAITING FOR MATCH START";
                    if (_coordinator != null)
                    {
                        switch (_coordinator.StateMachine.CurrentPhase)
                        {
                            case BattlePhase.WaitingForPlayer:
                                preBattleStatus = "WAITING FOR MATCH START";
                                break;
                            case BattlePhase.RoomCreated:
                                preBattleStatus = "ROOM CREATED";
                                break;
                            case BattlePhase.PlayerJoined:
                                preBattleStatus = "PLAYER CONNECTED";
                                break;
                            case BattlePhase.ARCalibration:
                                preBattleStatus = "CALIBRATING AR";
                                break;
                            case BattlePhase.GuildSelection:
                                preBattleStatus = "GUILD SELECTION";
                                break;
                            case BattlePhase.Loadout:
                                preBattleStatus = "LOADOUT SELECTION";
                                break;
                            case BattlePhase.BattleReady:
                                preBattleStatus = "MATCH READY";
                                break;
                            default:
                                preBattleStatus = _coordinator.StateMachine.CurrentPhase.ToString().ToUpper();
                                break;
                        }
                    }
                    turnIndicatorText.text = preBattleStatus;
                    turnIndicatorText.color = Color.white;
                }
                return;
            }

            bool isMyTurn = _coordinator.TurnManager.ActivePlayerId == _coordinator.LocalPlayer?.PlayerId;
            if (turnIndicatorText != null)
            {
                turnIndicatorText.text = isMyTurn ? "YOUR TURN" : "OPPONENT'S TURN";
                turnIndicatorText.color = isMyTurn ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.4f, 0.3f);
            }
        }

        private void HandleSpellSelectionRejected(string reason)
        {
            if (instructionBannerText != null)
            {
                instructionBannerText.text = $"[REJECTED] {reason.ToUpper()}";
                instructionBannerText.color = new Color(1f, 0.3f, 0.3f);
            }
            AppendLog($"[ACTION REJECTED] {reason}");
        }

        public void UpdateDebugButtonsState(BattlePhase phase)
        {
            bool isWaitingForPlayer = phase == BattlePhase.WaitingForPlayer;
            bool isRoomCreated = phase == BattlePhase.RoomCreated;
            bool isBattleReady = phase == BattlePhase.BattleReady;
            bool isSpellSelection = phase == BattlePhase.SpellSelection || phase == BattlePhase.VoiceProcessing;
            bool isGesture = phase == BattlePhase.GestureWindow && _coordinator?.SelectedSpell != null;
            bool isAim = phase == BattlePhase.AimPhase;
            bool inCombat = _coordinator != null && _coordinator.StateMachine.IsInBattleLoop;
            bool canReconnect = phase == BattlePhase.Reconnecting || phase == BattlePhase.ARTrackingLost || phase == BattlePhase.OpponentLost;
            bool notResolving = _coordinator == null || !_coordinator.IsResolvingAttack;

            // Dynamic voice selection label
            var activePlayer = _coordinator?.TurnManager?.GetActivePlayerState() ?? _coordinator?.LocalPlayer;
            string activeSpellName = (activePlayer != null && activePlayer.SpellSlots != null && activePlayer.SpellSlots.Length > 0 && activePlayer.SpellSlots[0] != null)
                ? activePlayer.SpellSlots[0].displayName
                : "Fireball";

            if (btnVoiceText != null)
            {
                btnVoiceText.text = $"4. Sim Voice: {activeSpellName}";
            }

            // Explicit Gesture & Spell Readouts
            string curSpellName = _coordinator?.SelectedSpell != null ? _coordinator.SelectedSpell.displayName : "None";
            string expectedGesture = _coordinator?.SelectedSpell != null ? _coordinator.SelectedSpell.gestureId : "None";
            string gestureShort = expectedGesture.Replace("GESTURE_", "");

            if (dbgCurrentSpellText != null)
                dbgCurrentSpellText.text = $"SPELL: {curSpellName}";
            if (dbgExpectedGestureText != null)
                dbgExpectedGestureText.text = $"GESTURE: {expectedGesture}";
            if (dbgGestureConfidenceText != null)
                dbgGestureConfidenceText.text = "CONFIDENCE: 95%";

            if (btnGestureSuccessText != null)
            {
                btnGestureSuccessText.text = _coordinator?.SelectedSpell != null
                    ? $"5. Sim Gesture: {gestureShort}"
                    : "5. Sim Gesture: None";
            }

            // 15 State-Gated Developer Controls:
            if (btnCreateRoom != null) btnCreateRoom.interactable = isWaitingForPlayer;
            if (btnJoinRoom != null) btnJoinRoom.interactable = isRoomCreated;
            if (btnStartBattle != null) btnStartBattle.interactable = isBattleReady || isWaitingForPlayer || phase == BattlePhase.PlayerJoined;

            if (btnVoice != null) btnVoice.interactable = isSpellSelection && notResolving;
            if (btnVoiceFail != null) btnVoiceFail.interactable = isSpellSelection && notResolving;
            if (btnTapSlot1 != null) btnTapSlot1.interactable = isSpellSelection && notResolving;

            if (btnGestureSuccess != null) btnGestureSuccess.interactable = isGesture && notResolving;
            if (btnGestureFail != null) btnGestureFail.interactable = isGesture && notResolving;
            if (btnGestureTimeout != null) btnGestureTimeout.interactable = isGesture && notResolving;

            if (btnAimLock != null) btnAimLock.interactable = isAim && notResolving;
            if (btnAimLost != null) btnAimLost.interactable = isAim && notResolving;
            if (btnLaunch != null) btnLaunch.interactable = isAim && notResolving;

            if (btnForceMiss != null) btnForceMiss.interactable = inCombat;
            if (btnForceCrit != null) btnForceCrit.interactable = inCombat;
            if (btnApplyStatus != null) btnApplyStatus.interactable = inCombat;

            if (btnDisconnect != null) btnDisconnect.interactable = inCombat;
            if (btnReconnect != null) btnReconnect.interactable = canReconnect;
            if (btnTeleport != null) btnTeleport.interactable = inCombat;
            if (btnQuickStart != null) btnQuickStart.interactable = true;
            if (btnResetBattle != null) btnResetBattle.interactable = true;

            if (btnToggleCpu != null && _coordinator != null)
            {
                if (btnToggleCpuText != null)
                {
                    btnToggleCpuText.text = $"CPU Opponent: {(_coordinator.IsCpuOpponentEnabled ? "[ON]" : "[OFF]")}";
                }
            }

            if (_coordinator?.ResolutionPipeline != null)
            {
                if (btnForceMissText != null)
                    btnForceMissText.text = $"8. Force Miss: {(_coordinator.ResolutionPipeline.ForceMissDebug ? "[ON]" : "[OFF]")}";
                if (btnForceCritText != null)
                    btnForceCritText.text = $"9. Force Crit: {(_coordinator.ResolutionPipeline.ForceCritDebug ? "[ON]" : "[OFF]")}";
            }

            UpdateSpellSlotsInteractability(isSpellSelection && notResolving);
            UpdateStatusDisplays();
        }

        private void UpdateSpellSlotsInteractability(bool isSpellSelection)
        {
            if (spellSlotButtons == null) return;
            var localPlayer = _coordinator?.LocalPlayer;

            for (int i = 0; i < spellSlotButtons.Length; i++)
            {
                if (spellSlotButtons[i] == null) continue;
                if (!isSpellSelection || localPlayer == null || i >= localPlayer.SpellSlots.Length)
                {
                    spellSlotButtons[i].interactable = false;
                }
                else
                {
                    var spell = localPlayer.SpellSlots[i];
                    bool canCast = spell != null && localPlayer.CanCastSpell(i, out _);
                    spellSlotButtons[i].interactable = canCast;
                }
            }
        }

        private void UpdateStatusDisplays()
        {
            if (playerStatusText != null && _coordinator?.LocalPlayer != null)
            {
                playerStatusText.text = _coordinator.LocalPlayer.GetActiveStatusesDisplay();
            }
            if (opponentStatusText != null && _coordinator?.OpponentPlayer != null)
            {
                opponentStatusText.text = _coordinator.OpponentPlayer.GetActiveStatusesDisplay();
            }
        }

        private void UpdateLocalHP(int cur, int max)
        {
            if (playerHpSlider == null) return;
            try
            {
                if (Application.isPlaying && isActiveAndEnabled)
                {
                    if (_localHpLerpCoroutine != null) StopCoroutine(_localHpLerpCoroutine);
                    _localHpLerpCoroutine = StartCoroutine(LerpHpSlider(playerHpSlider, playerHpText, cur, max, "HP"));
                    return;
                }
            }
            catch { }

            playerHpSlider.value = max > 0 ? (float)cur / max : 0;
            if (playerHpText != null) playerHpText.text = $"HP: {cur}/{max}";
        }

        private void UpdateLocalMana(int cur, int max)
        {
            if (playerManaSlider != null) playerManaSlider.value = max > 0 ? (float)cur / max : 0;
            if (playerManaText != null) playerManaText.text = $"MANA: {cur}/{max}";
        }

        private void UpdateOpponentHP(int cur, int max)
        {
            if (opponentHpSlider == null) return;
            try
            {
                if (Application.isPlaying && isActiveAndEnabled)
                {
                    if (_opponentHpLerpCoroutine != null) StopCoroutine(_opponentHpLerpCoroutine);
                    _opponentHpLerpCoroutine = StartCoroutine(LerpHpSlider(opponentHpSlider, opponentHpText, cur, max, "HP"));
                    return;
                }
            }
            catch { }

            opponentHpSlider.value = max > 0 ? (float)cur / max : 0;
            if (opponentHpText != null) opponentHpText.text = $"HP: {cur}/{max}";
        }

        private System.Collections.IEnumerator LerpHpSlider(Slider slider, Text label, int targetHp, int maxHp, string prefix)
        {
            float startVal = slider.value;
            float targetVal = maxHp > 0 ? (float)targetHp / maxHp : 0f;
            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curVal = Mathf.Lerp(startVal, targetVal, t);
                slider.value = curVal;
                if (label != null)
                {
                    int displayHp = Mathf.RoundToInt(curVal * maxHp);
                    label.text = $"{prefix}: {displayHp}/{maxHp}";
                }
                yield return null;
            }

            slider.value = targetVal;
            if (label != null) label.text = $"{prefix}: {targetHp}/{maxHp}";
        }

        private void UpdateOpponentMana(int cur, int max)
        {
            if (opponentManaSlider != null) opponentManaSlider.value = max > 0 ? (float)cur / max : 0;
            if (opponentManaText != null) opponentManaText.text = $"MANA: {cur}/{max}";
        }

        public void UpdateCastTimer(float secondsRemaining)
        {
            if (castTimerSlider != null) castTimerSlider.value = secondsRemaining;
            if (castTimerText != null) castTimerText.text = $"{secondsRemaining:0.0}s";

            if (_coordinator != null && _coordinator.StateMachine.CurrentPhase == BattlePhase.GestureWindow)
            {
                bool isMyTurn = _coordinator.TurnManager != null && _coordinator.TurnManager.ActivePlayerId == _coordinator.LocalPlayer?.PlayerId;
                if (instructionBannerText != null && isMyTurn)
                {
                    if (secondsRemaining <= 0.05f)
                    {
                        instructionBannerText.text = "GESTURE TIMEOUT";
                        instructionBannerText.color = new Color(1f, 0.35f, 0.35f);
                    }
                    else
                    {
                        float total = _coordinator.SelectedSpell != null && _coordinator.Config != null
                            ? _coordinator.Config.GetCastWindowForTier(_coordinator.SelectedSpell.tier)
                            : 3.0f;
                        int totalBlocks = 12;
                        int filled = Mathf.Clamp(Mathf.RoundToInt((secondsRemaining / Mathf.Max(0.1f, total)) * totalBlocks), 0, totalBlocks);
                        string bar = new string('█', filled) + new string('░', totalBlocks - filled);

                        instructionBannerText.text = $"RAISE YOUR HANDS ✋ ✋\n[GESTURE_THRUST]\n{bar} {secondsRemaining:0.0}s";
                        instructionBannerText.color = new Color(0.3f, 0.9f, 1f);
                    }
                }
            }
        }

        private void UpdateTargetReticle(ARTargetStatus status)
        {
            if (targetingReticle != null)
            {
                if (targetingReticle.sprite == null)
                {
                    targetingReticle.sprite = GetOrCreateReticleSprite();
                }
                targetingReticle.rectTransform.sizeDelta = new Vector2(72f, 72f);
                targetingReticle.color = status.isHitboxAimed 
                    ? new Color(0.2f, 1f, 0.4f, 0.85f) 
                    : (status.isVisible ? new Color(1f, 0.85f, 0.2f, 0.65f) : new Color(0.8f, 0.8f, 0.8f, 0.35f));
            }
            if (targetStatusText != null)
            {
                targetStatusText.text = status.isVisible ? $"TARGET LOCKED: {status.distanceMeters:0.0}m" : "SEARCHING FOR OPPONENT...";
                targetStatusText.color = status.isHitboxAimed ? Color.green : Color.yellow;
            }
        }

        public void AppendLog(string message)
        {
            if (combatLogText != null)
            {
                combatLogText.text = $"• {message}\n{combatLogText.text}";
                if (combatLogText.text.Length > 2000)
                {
                    combatLogText.text = combatLogText.text.Substring(0, 2000);
                }
            }
        }

        // =========================================================================
        // PROCEDURAL HUD GENERATOR (Guarantees HUD is never blank in Play Mode)
        // =========================================================================

        public void EnsureVisualHUD()
        {
            // If already wired from inspector, do not duplicate
            if (instructionBannerText != null && playerHpSlider != null &&
                spellSlotButtons != null && spellSlotButtons.Length == SpellSlotCount && spellSlotButtons[0] != null)
            {
                return;
            }

            if (runner == null)
            {
                runner = FindAnyObjectByType<GameFoundationRunner>();
            }

            // Find or create Canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }
            if (canvas == null)
            {
                var canvasGo = new GameObject("Battle_Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Ensure EventSystem
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                var inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputModuleType != null)
                {
                    esGo.AddComponent(inputModuleType);
                }
                else
                {
                    esGo.AddComponent<StandaloneInputModule>();
                }
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                        Resources.GetBuiltinResource<Font>("Arial.ttf") ??
                        Font.CreateDynamicFontFromOSFont("Arial", 14);

            Transform root = canvas.transform;

            // 1. TOP-LEFT: Local Player Panel
            var playerPanel = CreatePanel(root, "Panel_LocalPlayer", new Color(0.08f, 0.1f, 0.15f, 0.85f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, -100f), new Vector2(320f, 185f));
            _playerPanel = playerPanel;
            playerNameText = CreateText(playerPanel.transform, "Text_PlayerName", "PLAYER 1 (HOST)", font, 18, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -20f), new Vector2(-20f, 30f));
            playerGuildText = CreateText(playerPanel.transform, "Text_PlayerGuild", "Guild: Magma", font, 14, FontStyle.Italic, new Color(1f, 0.6f, 0.2f),
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -46f), new Vector2(-20f, 24f));
            playerHpSlider = CreateBar(playerPanel.transform, "Bar_PlayerHP", new Color(0.9f, 0.2f, 0.2f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -80f), new Vector2(-20f, 24f), out playerHpText, "HP: 1000/1000", font);
            playerManaSlider = CreateBar(playerPanel.transform, "Bar_PlayerMana", new Color(0.2f, 0.5f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -112f), new Vector2(-20f, 22f), out playerManaText, "MANA: 100/100", font);
            playerStatusText = CreateText(playerPanel.transform, "Text_PlayerStatuses", "Status: Normal", font, 11, FontStyle.Normal, new Color(0.8f, 0.9f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -140f), new Vector2(-20f, 22f));

            // 2. TOP-RIGHT: Opponent Player Panel
            var oppPanel = CreatePanel(root, "Panel_Opponent", new Color(0.15f, 0.08f, 0.1f, 0.85f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -100f), new Vector2(320f, 185f));
            _oppPanel = oppPanel;
            opponentNameText = CreateText(oppPanel.transform, "Text_OpponentName", "PLAYER 2 (OPPONENT)", font, 18, FontStyle.Bold, Color.white,
                TextAnchor.MiddleRight, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -20f), new Vector2(-20f, 30f));
            opponentGuildText = CreateText(oppPanel.transform, "Text_OpponentGuild", "Guild: Aqua", font, 14, FontStyle.Italic, new Color(0.3f, 0.8f, 1f),
                TextAnchor.MiddleRight, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -46f), new Vector2(-20f, 24f));
            opponentHpSlider = CreateBar(oppPanel.transform, "Bar_OpponentHP", new Color(0.9f, 0.2f, 0.2f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -80f), new Vector2(-20f, 24f), out opponentHpText, "HP: 1000/1000", font);
            opponentManaSlider = CreateBar(oppPanel.transform, "Bar_OpponentMana", new Color(0.2f, 0.5f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -112f), new Vector2(-20f, 22f), out opponentManaText, "MANA: 100/100", font);
            opponentStatusText = CreateText(oppPanel.transform, "Text_OpponentStatuses", "Status: Normal", font, 11, FontStyle.Normal, new Color(1f, 0.8f, 0.8f),
                TextAnchor.MiddleRight, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -140f), new Vector2(-20f, 22f));

            // 3. TOP-CENTER: Match Status Panel
            var statusPanel = CreatePanel(root, "Panel_Status", new Color(0.05f, 0.05f, 0.08f, 0.75f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -75f), new Vector2(340f, 130f));
            _statusPanel = statusPanel;
            turnIndicatorText = CreateText(statusPanel.transform, "Text_TurnIndicator", "YOUR TURN", font, 24, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(0f, 40f));
            turnNumberText = CreateText(statusPanel.transform, "Text_TurnNumber", "TURN 1", font, 16, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(0f, 30f));
            battlePhaseText = CreateText(statusPanel.transform, "Text_BattlePhase", "PHASE: WaitingForPlayer", font, 13, FontStyle.Normal, new Color(0.8f, 0.8f, 0.9f),
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -90f), new Vector2(0f, 30f));

            // 4. CENTER: Target Reticle & Cast Focus
            var centerPanel = CreatePanel(root, "Panel_CenterTargeting", new Color(0f, 0f, 0f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(360f, 360f));
            _centerPanel = centerPanel;
            targetingReticle = CreateImage(centerPanel.transform, "Image_Reticle", new Color(0.8f, 0.8f, 0.8f, 0.35f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            targetingReticle.sprite = GetOrCreateReticleSprite();
            targetStatusText = CreateText(centerPanel.transform, "Text_TargetStatus", "SEARCHING FOR OPPONENT...", font, 15, FontStyle.Bold, Color.yellow,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(360f, 26f));
            selectedSpellText = CreateText(centerPanel.transform, "Text_SelectedSpell", "Spell: None", font, 15, FontStyle.Bold, new Color(0.4f, 0.9f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(360f, 30f));
            castTimerSlider = CreateBar(centerPanel.transform, "Bar_CastTimer", new Color(1f, 0.75f, 0.1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(240f, 16f), out castTimerText, "5.0s", font);

            // 5. CENTER-BOTTOM: Instruction Banner & Combat Result
            var bannerPanel = CreatePanel(root, "Panel_InstructionBanner", new Color(0.06f, 0.08f, 0.16f, 0.92f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(760f, 100f));
            _bannerPanel = bannerPanel;
            instructionBannerText = CreateText(bannerPanel.transform, "Text_Instruction", "YOUR TURN - SAY SPELL NAME (OR TAP BUTTON BELOW)", font, 18, FontStyle.Bold, new Color(1f, 0.9f, 0.3f),
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(0f, 40f));
            combatResultText = CreateText(bannerPanel.transform, "Text_CombatResult", "Awaiting duel initiation...", font, 15, FontStyle.Normal, new Color(0.8f, 0.9f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 25f), new Vector2(0f, 36f));

            // 6. BOTTOM: 6 Spell Slots (Slots 1-5 + Slot 6 Fallback)
            var spellPanel = CreatePanel(root, "Panel_SpellSlots", new Color(0.05f, 0.06f, 0.1f, 0.9f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 85f), new Vector2(980f, 130f));
            _spellPanel = spellPanel;
            float btnWidth = 145f;
            float btnSpacing = 160f;
            float startX = -((SpellSlotCount - 1) * btnSpacing) * 0.5f;

            string[] defaultSpellNames = new[] { "Ember Lance\n(15 MP)", "Flame Ward\n(20 MP)", "Fireball\n(25 MP)", "Magma Surge\n(35 MP)", "Pyro Blast\n(40 MP)", "Struggle\n(0 MP)\n[Fallback]" };

            if (spellSlotButtons == null || spellSlotButtons.Length != SpellSlotCount)
            {
                spellSlotButtons = new Button[SpellSlotCount];
            }
            if (spellSlotTexts == null || spellSlotTexts.Length != SpellSlotCount)
            {
                spellSlotTexts = new Text[SpellSlotCount];
            }

            for (int i = 0; i < SpellSlotCount; i++)
            {
                int index = i;
                Color btnBg = (i == SpellSlotCount - 1) ? new Color(0.35f, 0.2f, 0.15f, 0.95f) : new Color(0.12f, 0.22f, 0.38f, 0.95f);
                string defaultName = (defaultSpellNames != null && i < defaultSpellNames.Length) ? defaultSpellNames[i] : $"Slot {i + 1}";
                var btn = CreateButton(spellPanel.transform, $"Btn_Slot_{i + 1}", defaultName, font, 13, btnBg, Color.white,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(startX + (i * btnSpacing), 0f), new Vector2(btnWidth, 105f),
                    () =>
                    {
                        if (_coordinator != null)
                        {
                            _coordinator.SelectSpellManualFallback(index);
                        }
                        AppendLog($"[UI] Spell Slot {index + 1} clicked.");
                    });

                spellSlotButtons[i] = btn;
                spellSlotTexts[i] = btn.GetComponentInChildren<Text>();
            }

            // Quick Action Button: Center-bottom, above spell panel for one-tap attack launch / gesture confirm
            btnActionButton = CreateButton(root, "Btn_QuickAction", "⚡ LAUNCH ATTACK [TAP TO CAST] ⚡", font, 16,
                new Color(0.85f, 0.22f, 0.15f), Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 175f), new Vector2(460f, 48f),
                () =>
                {
                    if (_coordinator == null) return;
                    var phase = _coordinator.StateMachine.CurrentPhase;
                    if (phase == BattlePhase.AimPhase)
                    {
                        runner?.TestAttackLaunch();
                        AppendLog("[ACTION] Attack launch committed via screen tap!");
                    }
                    else if (phase == BattlePhase.GestureWindow)
                    {
                        runner?.TestHandsUp();
                        AppendLog("[ACTION] Hands-Up (GESTURE_THRUST) confirmed via screen tap!");
                    }
                    else if (phase == BattlePhase.SpellSelection || phase == BattlePhase.PlayerTurn)
                    {
                        runner?.SimulateVoiceForActivePlayer();
                    }
                });
            btnActionButtonText = btnActionButton.GetComponentInChildren<Text>();
            btnActionButton.gameObject.SetActive(false);

            // Make reticle and banner clickable for attack commitment
            if (targetingReticle != null)
            {
                var reticleBtn = targetingReticle.gameObject.AddComponent<Button>();
                reticleBtn.onClick.AddListener(() =>
                {
                    if (_coordinator?.StateMachine?.CurrentPhase == BattlePhase.AimPhase)
                    {
                        runner?.TestAttackLaunch();
                        AppendLog("[AIM] Reticle tapped: Attack launch committed!");
                    }
                });
            }
            if (bannerPanel != null)
            {
                var bannerBtn = bannerPanel.AddComponent<Button>();
                bannerBtn.onClick.AddListener(() =>
                {
                    if (_coordinator?.StateMachine?.CurrentPhase == BattlePhase.AimPhase)
                    {
                        runner?.TestAttackLaunch();
                        AppendLog("[BANNER] Banner tapped: Attack launch committed!");
                    }
                    else if (_coordinator?.StateMachine?.CurrentPhase == BattlePhase.GestureWindow)
                    {
                        runner?.SimulateCurrentSpellGesture(0.95f);
                        AppendLog("[BANNER] Banner tapped: Gesture confirmed!");
                    }
                });
            }

            // 7. MIDDLE-LEFT: Combat Log
            var logPanel = CreatePanel(root, "Panel_CombatLog", new Color(0.04f, 0.05f, 0.08f, 0.85f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -20f), new Vector2(280f, 260f));
            _logPanel = logPanel;
            CreateText(logPanel.transform, "Header_Log", "COMBAT LOG", font, 14, FontStyle.Bold, new Color(0.7f, 0.8f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -14f), new Vector2(-20f, 24f));
            combatLogText = CreateText(logPanel.transform, "Text_LogContent", "Duel log initialized.\nReady for combat.", font, 12, FontStyle.Normal, new Color(0.85f, 0.85f, 0.85f),
                TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, -36f), new Vector2(-20f, -46f));

            // VFX Container for projectiles and floating numbers
            var vfxGo = new GameObject("Container_VFX", typeof(RectTransform));
            vfxGo.transform.SetParent(root, false);
            var vfxRect = vfxGo.GetComponent<RectTransform>();
            vfxRect.anchorMin = Vector2.zero;
            vfxRect.anchorMax = Vector2.one;
            vfxRect.sizeDelta = Vector2.zero;
            vfxContainer = vfxRect;

            floatingDamageText = CreateText(vfxContainer, "Text_FloatingDamage", "", font, 24, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 80f));
            floatingDamageText.gameObject.SetActive(false);

            // Combat Diagnostic Panel (Bottom-Left)
            panelDiagnostics = CreatePanel(root, "Panel_Diagnostics", new Color(0.02f, 0.04f, 0.08f, 0.92f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(170f, 190f), new Vector2(320f, 360f));
            CreateText(panelDiagnostics.transform, "Header_Diag", "COMBAT DIAGNOSTICS", font, 12, FontStyle.Bold, new Color(0.3f, 0.9f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -10f), new Vector2(0f, 20f));

            diagHpTraceText = CreateText(panelDiagnostics.transform, "Diag_HpTrace", "[HP TRACE]\nDefender HP: 1000 -> 1000", font, 10, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -32f), new Vector2(-16f, 32f));
            diagManaTraceText = CreateText(panelDiagnostics.transform, "Diag_ManaTrace", "[MANA TRACE]\nMana: 100 -> Cost: 0 -> 100", font, 10, FontStyle.Normal, new Color(0.5f, 0.8f, 1f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -66f), new Vector2(-16f, 32f));
            diagMissFormulaText = CreateText(panelDiagnostics.transform, "Diag_MissFormula", "[MISS FORMULA]\nBase: 7.5% -> Final: 7.5%", font, 10, FontStyle.Normal, new Color(1f, 0.8f, 0.4f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -100f), new Vector2(-16f, 32f));
            diagDamageFormulaText = CreateText(panelDiagnostics.transform, "Diag_DamageFormula", "[DAMAGE FORMULA]\nBase x Type x (1+Bonus) x Crit = DMG", font, 10, FontStyle.Normal, new Color(1f, 0.5f, 0.4f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -134f), new Vector2(-16f, 32f));
            diagModifiersText = CreateText(panelDiagnostics.transform, "Diag_Modifiers", "[ACTIVE MODIFIERS]\nGuild / Terrain (+25%)", font, 10, FontStyle.Normal, new Color(0.8f, 0.7f, 1f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -168f), new Vector2(-16f, 32f));
            diagStatusesText = CreateText(panelDiagnostics.transform, "Diag_Statuses", "[STATUSES]\nAttacker: Normal | Defender: Normal", font, 10, FontStyle.Normal, new Color(0.7f, 1f, 0.7f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -202f), new Vector2(-16f, 34f));
            diagHandTrackingText = CreateText(panelDiagnostics.transform, "Diag_HandTracking",
                "[HAND TRACKING DIAGNOSTICS]\nHand Tracking: OFF\nHands Detected: 0\nHands Up: NO\nConfidence: 0.0%\nGesture: NONE\nGesture Timer: 0.0 s\nGesture Result: WAITING",
                font, 10, FontStyle.Normal, new Color(0.3f, 0.9f, 1f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -238f), new Vector2(-16f, 110f));

            // 8. Live Camera Feed Panel (Bottom-Right)
            panelCameraFeed = CreatePanel(root, "Panel_CameraFeed", new Color(0.02f, 0.04f, 0.08f, 0.94f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-160f, 160f), new Vector2(300f, 260f));
            panelCameraFeed.SetActive(false);

            CreateText(panelCameraFeed.transform, "Header_CameraFeed", "LIVE CAMERA & GESTURE TRACKER", font, 11, FontStyle.Bold, new Color(0.3f, 0.9f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(0f, 18f));

            var viewportGo = new GameObject("CameraViewport", typeof(RectTransform), typeof(RawImage), typeof(Button));
            viewportGo.transform.SetParent(panelCameraFeed.transform, false);
            var vpRect = viewportGo.GetComponent<RectTransform>();
            vpRect.anchorMin = new Vector2(0.05f, 0.22f);
            vpRect.anchorMax = new Vector2(0.95f, 0.88f);
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            cameraFeedRawImage = viewportGo.GetComponent<RawImage>();
            cameraFeedRawImage.color = Color.white;
            cameraFeedRawImage.uvRect = new Rect(1, 0, -1, 1);

            var vpBtn = viewportGo.GetComponent<Button>();
            vpBtn.onClick.AddListener(() =>
            {
                if (_coordinator?.StateMachine?.CurrentPhase == BattlePhase.GestureWindow)
                {
                    runner?.TestHandsUp();
                    AppendLog("[CAMERA FEED] Tapped camera viewport: Hands-Up confirmed!");
                }
            });

            var reticleGo = new GameObject("HandTrackingReticle", typeof(RectTransform), typeof(Image));
            reticleGo.transform.SetParent(viewportGo.transform, false);
            var reticleRect = reticleGo.GetComponent<RectTransform>();
            reticleRect.anchorMin = new Vector2(0.5f, 0.5f);
            reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRect.sizeDelta = new Vector2(40f, 40f);
            cameraHandReticle = reticleGo.GetComponent<Image>();
            cameraHandReticle.color = new Color(0.2f, 1f, 0.4f, 0.85f);
            cameraHandReticle.gameObject.SetActive(false);

            textCameraStatus = CreateText(panelCameraFeed.transform, "Text_CameraStatus", "Tracking: Standby", font, 10, FontStyle.Normal, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(0f, 24f));

            // Guild Selection Overlay
            panelGuildSelection = CreatePanel(root, "Panel_GuildSelection", new Color(0.04f, 0.05f, 0.1f, 0.98f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelGuildSelection.SetActive(false);

            CreateText(panelGuildSelection.transform, "Header_GuildSelect", "CHOOSE YOUR GUILD", font, 26, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 50f));

            string[] guildIds = new[] { "MAGMA", "AQUA", "PLASMA", "HOLY_TRINITY", "GAMBIT" };
            Color[] guildColors = new[] {
                new Color(0.8f, 0.3f, 0.15f),
                new Color(0.15f, 0.45f, 0.8f),
                new Color(0.6f, 0.3f, 0.8f),
                new Color(0.8f, 0.75f, 0.2f),
                new Color(0.2f, 0.65f, 0.5f)
            };

            float cardSpacing = 160f;
            float cardStartX = -((guildIds.Length - 1) * cardSpacing) * 0.5f;

            for (int g = 0; g < guildIds.Length; g++)
            {
                string gid = guildIds[g];
                var guildData = AuthoritativeCombatRoster.GetGuild(gid);
                float x = cardStartX + (g * cardSpacing);

                CreateButton(panelGuildSelection.transform, $"Card_{gid}", guildData?.displayName ?? gid, font, 14, guildColors[g], Color.white,
                    new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(x, 0f), new Vector2(145f, 120f),
                    () =>
                    {
                        _selectedGuild = guildData;
                        if (textSelectedGuildTitle != null) textSelectedGuildTitle.text = guildData.displayName.ToUpper();
                        if (textSelectedGuildAffinity != null)
                        {
                            string aff = (guildData.affinityTypes != null && guildData.affinityTypes.Count > 0)
                                ? string.Join(", ", guildData.affinityTypes)
                                : "None (Gambit)";
                            textSelectedGuildAffinity.text = $"Affinity: {aff}";
                        }
                        if (textSelectedGuildPassive != null)
                        {
                            textSelectedGuildPassive.text = guildData.isGambitGuild
                                ? "Passive: 45% Critical Chance, 1.25x Crit Multiplier, Slot 6 Bluff"
                                : $"Passive: +{guildData.offensiveBonus * 100f:0}% Elemental Bonus on matching spells";
                        }
                        if (textSelectedGuildPlaystyle != null)
                        {
                            if (gid == "MAGMA") textSelectedGuildPlaystyle.text = "Archetype: Aggressive burst damage, self-damage recoil mages.";
                            else if (gid == "AQUA") textSelectedGuildPlaystyle.text = "Archetype: High sustain healing, freezes, and control play.";
                            else if (gid == "PLASMA") textSelectedGuildPlaystyle.text = "Archetype: High speed, paralyzing control, and Move Skip.";
                            else if (gid == "HOLY_TRINITY") textSelectedGuildPlaystyle.text = "Archetype: Death Light executioners and life-burning casters.";
                            else textSelectedGuildPlaystyle.text = "Archetype: High-stakes gamblers, massive crits, and dynamic Bluff.";
                        }
                    });
            }

            textSelectedGuildTitle = CreateText(panelGuildSelection.transform, "Text_GuildTitle", "MAGMA", font, 22, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(600f, 35f));
            textSelectedGuildAffinity = CreateText(panelGuildSelection.transform, "Text_GuildAffinity", "Affinity: Fire, Ground", font, 16, FontStyle.Normal, new Color(0.8f, 0.9f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.40f), new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(600f, 30f));
            textSelectedGuildPassive = CreateText(panelGuildSelection.transform, "Text_GuildPassive", "Passive: +25% Elemental Bonus on Fire and Ground spells", font, 14, FontStyle.Italic, new Color(1f, 0.85f, 0.4f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(600f, 30f));
            textSelectedGuildPlaystyle = CreateText(panelGuildSelection.transform, "Text_GuildPlaystyle", "Archetype: Aggressive burst damage, self-damage recoil mages.", font, 14, FontStyle.Normal, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.30f), new Vector2(0.5f, 0.30f), Vector2.zero, new Vector2(600f, 40f));

            btnConfirmGuild = CreateButton(panelGuildSelection.transform, "Btn_ConfirmGuild", "CONFIRM GUILD SELECTION", font, 16, new Color(0.2f, 0.6f, 0.3f), Color.white,
                new Vector2(0.5f, 0.20f), new Vector2(0.5f, 0.20f), Vector2.zero, new Vector2(320f, 50f),
                () =>
                {
                    if (_selectedGuild == null) _selectedGuild = AuthoritativeCombatRoster.GetGuild("MAGMA");
                    _coordinator?.ConfirmGuildSelection(_selectedGuild);
                    panelGuildSelection.SetActive(false);
                });

            // Battle Conclusion Overlay
            panelBattleConclusion = CreatePanel(root, "Panel_BattleConclusion", new Color(0.04f, 0.04f, 0.08f, 0.96f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelBattleConclusion.SetActive(false);

            textConclusionTitle = CreateText(panelBattleConclusion.transform, "Text_ConclusionTitle", "VICTORY!", font, 36, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(600f, 60f));
            textConclusionWinner = CreateText(panelBattleConclusion.transform, "Text_ConclusionWinner", "Player 1 Wins!", font, 20, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(600f, 40f));
            textConclusionDetails = CreateText(panelBattleConclusion.transform, "Text_ConclusionDetails", "Total Turns: 8\nYour HP: 450/1000 | Opponent HP: 0/1000", font, 16, FontStyle.Normal, new Color(0.8f, 0.9f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(600f, 60f));

            btnConclusionRestart = CreateButton(panelBattleConclusion.transform, "Btn_RestartBattle", "PLAY AGAIN / REMATCH", font, 16, new Color(0.25f, 0.55f, 0.35f), Color.white,
                new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), new Vector2(-130f, 0f), new Vector2(240f, 50f),
                () =>
                {
                    panelBattleConclusion.SetActive(false);
                    runner?.ResetMatch();
                    runner?.EnsureMatchInitialized();
                    runner?.RestartMatchQuickStart();
                    ShowScreen(ScreenState.CombatHUD);
                });

            btnConclusionMainMenu = CreateButton(panelBattleConclusion.transform, "Btn_ConclusionMainMenu", "MAIN MENU", font, 16, new Color(0.18f, 0.38f, 0.72f), Color.white,
                new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), new Vector2(130f, 0f), new Vector2(200f, 50f),
                () =>
                {
                    panelBattleConclusion.SetActive(false);
                    runner?.ResetMatch();
                    ShowScreen(ScreenState.MainMenu);
                });

            // 8. MIDDLE-RIGHT: 15 State-Aware Developer Controls & CPU Opponent
            var debugPanel = CreatePanel(root, "Panel_DebugControls", new Color(0.08f, 0.08f, 0.12f, 0.92f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-125f, 0f), new Vector2(230f, 620f));
            _debugPanel = debugPanel;
            CreateText(debugPanel.transform, "Header_Debug", "DEV CONTROLS (15 ACTIONS)", font, 12, FontStyle.Bold, new Color(1f, 0.75f, 0.3f),
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -10f), new Vector2(0f, 20f));

            // Explicit Gesture & Spell Readouts
            dbgCurrentSpellText = CreateText(debugPanel.transform, "Dbg_CurrentSpell", "SPELL: None", font, 10, FontStyle.Bold, new Color(0.4f, 0.9f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -28f), new Vector2(-20f, 16f));
            dbgExpectedGestureText = CreateText(debugPanel.transform, "Dbg_ExpectedGesture", "GESTURE: None", font, 10, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -44f), new Vector2(-20f, 16f));
            dbgGestureConfidenceText = CreateText(debugPanel.transform, "Dbg_GestureConfidence", "CONFIDENCE: 95%", font, 10, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f),
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -60f), new Vector2(-20f, 16f));

            float dbgY = -80f;
            float dbgStep = 26f;
            Vector2 dbgBtnSize = new Vector2(210f, 23f);

            // 1. Create Room
            btnCreateRoom = CreateButton(debugPanel.transform, "Dbg_CreateRoom", "1. Create Room", font, 10, new Color(0.2f, 0.35f, 0.55f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestCreateRoom());
            dbgY -= dbgStep;

            // 2. Join Room
            btnJoinRoom = CreateButton(debugPanel.transform, "Dbg_JoinRoom", "2. Join Room", font, 10, new Color(0.25f, 0.45f, 0.45f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestJoinRoom());
            dbgY -= dbgStep;

            // 3. Start Battle
            btnStartBattle = CreateButton(debugPanel.transform, "Dbg_StartBattle", "3. Start Battle", font, 10, new Color(0.2f, 0.5f, 0.3f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestStartBattle());
            dbgY -= dbgStep;

            // 4. Sim Voice
            btnVoice = CreateButton(debugPanel.transform, "Dbg_Voice", "4. Sim Voice: Fireball", font, 10, new Color(0.18f, 0.42f, 0.28f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.SimulateVoiceForActivePlayer());
            btnVoiceText = btnVoice.GetComponentInChildren<Text>();
            dbgY -= dbgStep;

            // 5. Sim Gesture
            btnGestureSuccess = CreateButton(debugPanel.transform, "Dbg_GestureSuccess", "5. Sim Gesture: Success", font, 10, new Color(0.18f, 0.45f, 0.5f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.SimulateCurrentSpellGesture(0.95f));
            btnGestureSuccessText = btnGestureSuccess.GetComponentInChildren<Text>();
            dbgY -= dbgStep;

            // 6. Sim Aim
            btnAimLock = CreateButton(debugPanel.transform, "Dbg_AimLock", "6. Sim Aim: Lock 4.5m", font, 10, new Color(0.5f, 0.4f, 0.15f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestTargetAcquired());
            dbgY -= dbgStep;

            // 7. Launch Attack
            btnLaunch = CreateButton(debugPanel.transform, "Dbg_Launch", "7. Commit Attack Launch", font, 10, new Color(0.65f, 0.2f, 0.2f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestAttackLaunch());
            dbgY -= dbgStep;

            // 8. Force Miss
            btnForceMiss = CreateButton(debugPanel.transform, "Dbg_ForceMiss", "8. Force Miss: [OFF]", font, 10, new Color(0.35f, 0.2f, 0.45f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () =>
                {
                    if (_coordinator?.ResolutionPipeline != null)
                    {
                        _coordinator.ResolutionPipeline.ForceMissDebug = !_coordinator.ResolutionPipeline.ForceMissDebug;
                        UpdateDebugButtonsState(_coordinator.StateMachine.CurrentPhase);
                    }
                });
            btnForceMissText = btnForceMiss.GetComponentInChildren<Text>();
            dbgY -= dbgStep;

            // 9. Force Crit
            btnForceCrit = CreateButton(debugPanel.transform, "Dbg_ForceCrit", "9. Force Crit: [OFF]", font, 10, new Color(0.45f, 0.3f, 0.15f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () =>
                {
                    if (_coordinator?.ResolutionPipeline != null)
                    {
                        _coordinator.ResolutionPipeline.ForceCritDebug = !_coordinator.ResolutionPipeline.ForceCritDebug;
                        UpdateDebugButtonsState(_coordinator.StateMachine.CurrentPhase);
                    }
                });
            btnForceCritText = btnForceCrit.GetComponentInChildren<Text>();
            dbgY -= dbgStep;

            // 10. Apply Status
            btnApplyStatus = CreateButton(debugPanel.transform, "Dbg_ApplyStatus", "10. Apply Status (Cycle)", font, 10, new Color(0.5f, 0.2f, 0.4f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestApplyStatus());
            dbgY -= dbgStep;

            // 11. Gesture Timeout
            btnGestureTimeout = CreateButton(debugPanel.transform, "Dbg_GestureTimeout", "11. Trigger Timeout", font, 10, new Color(0.45f, 0.35f, 0.2f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestGestureTimeout());
            dbgY -= dbgStep;

            // 12. Disconnect
            btnDisconnect = CreateButton(debugPanel.transform, "Dbg_Disconnect", "12. Sim Disconnect", font, 10, new Color(0.45f, 0.2f, 0.3f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestDisconnect());
            dbgY -= dbgStep;

            // 13. Reconnect
            btnReconnect = CreateButton(debugPanel.transform, "Dbg_Reconnect", "13. Sim Reconnect", font, 10, new Color(0.2f, 0.45f, 0.45f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestReconnect());
            dbgY -= dbgStep;

            // 14. Teleport / Forfeit
            btnTeleport = CreateButton(debugPanel.transform, "Dbg_Teleport", "14. Sim Teleport / Forfeit", font, 10, new Color(0.4f, 0.3f, 0.5f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.TestTeleport());
            dbgY -= dbgStep;

            // 15. Reset Battle
            btnResetBattle = CreateButton(debugPanel.transform, "Dbg_ResetBattle", "15. RESET BATTLE", font, 10, new Color(0.55f, 0.15f, 0.15f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () => runner?.ResetMatch());
            dbgY -= dbgStep;

            // CPU Opponent Toggle
            btnToggleCpu = CreateButton(debugPanel.transform, "Dbg_ToggleCpu", "CPU Opponent: [OFF]", font, 10, new Color(0.2f, 0.4f, 0.5f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () =>
                {
                    runner?.ToggleCpuOpponent();
                    UpdateDebugButtonsState(_coordinator?.StateMachine?.CurrentPhase ?? BattlePhase.WaitingForPlayer);
                });
            btnToggleCpuText = btnToggleCpu.GetComponentInChildren<Text>();
            dbgY -= dbgStep;

            // Toggle Diagnostics
            btnToggleDiagnostics = CreateButton(debugPanel.transform, "Dbg_ToggleDiag", "[Toggle Diagnostics]", font, 10, new Color(0.2f, 0.25f, 0.35f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () =>
                {
                    if (panelDiagnostics != null)
                    {
                        panelDiagnostics.SetActive(!panelDiagnostics.activeSelf);
                    }
                });

            // Legacy backward-compatible hooks (hidden or wired)
            btnVoiceFail = CreateButton(debugPanel.transform, "Dbg_VoiceFail", "", font, 1, Color.clear, Color.clear, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, () => runner?.TestVoiceUnrecognized());
            btnTapSlot1 = CreateButton(debugPanel.transform, "Dbg_TapSlot1", "", font, 1, Color.clear, Color.clear, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, () => runner?.TestTapSlot1());
            btnGestureFail = CreateButton(debugPanel.transform, "Dbg_GestureFail", "", font, 1, Color.clear, Color.clear, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, () => runner?.TestGestureFail());
            btnAimLost = CreateButton(debugPanel.transform, "Dbg_AimLost", "", font, 1, Color.clear, Color.clear, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, () => runner?.TestTargetLost());
            btnQuickStart = CreateButton(debugPanel.transform, "Dbg_QuickStart", "", font, 1, Color.clear, Color.clear, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, () => runner?.RestartMatchQuickStart());

            // 14-Metric Dev Overlay Panel (Top-Center below header)
            panelDevMetrics = CreatePanel(root, "Panel_DevMetrics", new Color(0.01f, 0.03f, 0.06f, 0.94f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(480f, 380f));
            textDevMetrics = CreateText(panelDevMetrics.transform, "Text_DevMetrics", "[14-METRIC DEV OVERLAY INITIALIZING...]", font, 11, FontStyle.Normal, Color.green,
                TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(12f, -12f), new Vector2(-24f, -50f));
            CreateButton(panelDevMetrics.transform, "Btn_CloseMetrics", "CLOSE DEV OVERLAY", font, 12, new Color(0.6f, 0.2f, 0.2f), Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(200f, 32f), () => panelDevMetrics.SetActive(false));
            panelDevMetrics.SetActive(false);

            // Toggle Dev Metrics button on dev panel
            btnToggleDevMetrics = CreateButton(debugPanel.transform, "Dbg_ToggleMetrics", "[14-Metric Dev Overlay]", font, 10, new Color(0.15f, 0.35f, 0.45f), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, dbgY), dbgBtnSize, () =>
                {
                    if (panelDevMetrics != null) panelDevMetrics.SetActive(!panelDevMetrics.activeSelf);
                });

            // Shared Reference Calibration Overlay
            panelCalibration = CreatePanel(root, "Panel_Calibration", new Color(0.03f, 0.05f, 0.1f, 0.96f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelCalibration.SetActive(false);

            CreateText(panelCalibration.transform, "Header_Calibration", "SHARED BATTLE SPACE CALIBRATION", font, 24, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 50f));

            textCalibrationInstruction = CreateText(panelCalibration.transform, "Text_CalibInstruction",
                "STEP 1: POINT AR CAMERA AT SHARED PHYSICAL REFERENCE\n(Both players must look at the same floor or table reference marker)",
                font, 16, FontStyle.Normal, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(640f, 120f));

            btnConfirmOrigin = CreateButton(panelCalibration.transform, "Btn_ConfirmOrigin", "CONFIRM REFERENCE ORIGIN", font, 16, new Color(0.2f, 0.6f, 0.3f), Color.white,
                new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(340f, 50f), () =>
                {
                    if (_coordinator?.ARTracking is ARFoundationTrackingService arFound && arFound.calibrator != null)
                    {
                        arFound.calibrator.LockDetectedReference(Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 2f : new Vector3(0, 0, 2f), Quaternion.identity);
                    }
                });

            btnDevManualOriginFallback = CreateButton(panelCalibration.transform, "Btn_ManualOriginFallback", "DEV MODE: MANUAL ORIGIN ALIGN (FALLBACK)", font, 12, new Color(0.6f, 0.35f, 0.15f), Color.white,
                new Vector2(0.5f, 0.26f), new Vector2(0.5f, 0.26f), Vector2.zero, new Vector2(340f, 40f), () =>
                {
                    if (_coordinator?.ARTracking is ARFoundationTrackingService arFound && arFound.calibrator != null)
                    {
                        arFound.calibrator.SetManualOriginFallback(Vector3.zero, Quaternion.identity);
                    }
                    panelCalibration.SetActive(false);
                });

            // 9. TOP-LEVEL MAIN MENU OVERLAY
            panelMainMenu = CreatePanel(root, "Panel_MainMenu", new Color(0.04f, 0.05f, 0.1f, 0.96f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateText(panelMainMenu.transform, "Title_Game", "AR GUILD ENCOUNTER", font, 36, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(700f, 60f));

            CreateText(panelMainMenu.transform, "Subtitle_Game", "MOBILE 1V1 AR SPELL DUEL", font, 16, FontStyle.Italic, new Color(0.3f, 0.85f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(600f, 30f));

            CreateButton(panelMainMenu.transform, "Btn_MenuVsCpu", "VS CPU DUEL", font, 18, new Color(0.18f, 0.52f, 0.32f), Color.white,
                new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(340f, 55f),
                () =>
                {
                    runner?.LaunchVsCpu();
                    ShowScreen(ScreenState.CombatHUD);
                });

            CreateButton(panelMainMenu.transform, "Btn_MenuPlayPvP", "PLAY PVP (2 PLAYERS)", font, 18, new Color(0.18f, 0.38f, 0.72f), Color.white,
                new Vector2(0.5f, 0.40f), new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(340f, 55f),
                () =>
                {
                    ShowScreen(ScreenState.Matchmaking);
                });

            CreateButton(panelMainMenu.transform, "Btn_MenuDevQuickStart", "DEV QUICK-START / TEST", font, 14, new Color(0.45f, 0.35f, 0.2f), Color.white,
                new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(340f, 45f),
                () =>
                {
                    runner?.RestartMatchQuickStart();
                    ShowScreen(ScreenState.CombatHUD);
                });

            // 10. PVP MATCHMAKING PANEL
            panelMatchmaking = CreatePanel(root, "Panel_Matchmaking", new Color(0.04f, 0.05f, 0.1f, 0.96f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelMatchmaking.SetActive(false);

            CreateText(panelMatchmaking.transform, "Header_Matchmaking", "PVP MATCHMAKING", font, 28, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(600f, 50f));

            textPvPStatus = CreateText(panelMatchmaking.transform, "Text_PvPStatus", "Choose Host or Join to start 2-device duel.", font, 14, FontStyle.Normal, new Color(0.8f, 0.9f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.77f), new Vector2(0.5f, 0.77f), Vector2.zero, new Vector2(600f, 30f));

            btnHostCreate = CreateButton(panelMatchmaking.transform, "Btn_HostCreate", "CREATE 6-DIGIT ROOM (HOST)", font, 16, new Color(0.2f, 0.5f, 0.35f), Color.white,
                new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(380f, 50f),
                () =>
                {
                    if (textPvPStatus != null) textPvPStatus.text = "Broadcasting UDP room beacon...";
                    _coordinator?.NetworkSession.CreateRoom("Player_1", (success, code) =>
                    {
                        if (success)
                        {
                            if (textWaitingRoomCode != null) textWaitingRoomCode.text = $"ROOM CODE: {code}";
                            if (textWaitingStatus != null) textWaitingStatus.text = "Room broadcast active. Waiting for Player 2...";
                            if (btnWaitingStartDuel != null) btnWaitingStartDuel.interactable = false;
                            ShowScreen(ScreenState.WaitingRoom);
                        }
                        else
                        {
                            if (textPvPStatus != null) textPvPStatus.text = $"Room creation failed: {code}";
                        }
                    });
                });

            CreateText(panelMatchmaking.transform, "Label_RoomCode", "ENTER 6-DIGIT CODE TO JOIN:", font, 13, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(380f, 25f));

            inputRoomCode = CreateInputField(panelMatchmaking.transform, "Input_RoomCode", "e.g. 123456", font, 16,
                new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(260f, 40f));

            CreateText(panelMatchmaking.transform, "Label_ManualIp", "OR ENTER MANUAL HOST IP (ROUTER AP ISOLATION FALLBACK):", font, 11, FontStyle.Normal, new Color(0.7f, 0.8f, 0.9f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(460f, 25f));

            inputManualIp = CreateInputField(panelMatchmaking.transform, "Input_ManualIp", "e.g. 192.168.1.50", font, 14,
                new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(260f, 36f));

            btnClientJoin = CreateButton(panelMatchmaking.transform, "Btn_ClientJoin", "JOIN ROOM", font, 16, new Color(0.2f, 0.4f, 0.65f), Color.white,
                new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(260f, 45f),
                () =>
                {
                    string code = inputRoomCode != null && !string.IsNullOrEmpty(inputRoomCode.text) ? inputRoomCode.text.Trim() : "123456";
                    string ip = inputManualIp != null ? inputManualIp.text.Trim() : "";

                    if (textPvPStatus != null) textPvPStatus.text = $"Connecting to room {code}...";

                    if (!string.IsNullOrEmpty(ip) && _coordinator?.NetworkSession is LocalPvPPrototypeTransport transport)
                    {
                        transport.ConnectWithManualIp(ip, code, "Player_2", (success, msg) =>
                        {
                            if (success)
                            {
                                if (textWaitingRoomCode != null) textWaitingRoomCode.text = $"ROOM CODE: {code}";
                                if (textWaitingStatus != null) textWaitingStatus.text = "Connected! Calibrating shared AR space...";
                                if (btnWaitingStartDuel != null) btnWaitingStartDuel.interactable = true;
                                ShowScreen(ScreenState.WaitingRoom);
                            }
                            else
                            {
                                if (textPvPStatus != null) textPvPStatus.text = $"Connection error: {msg}";
                            }
                        });
                    }
                    else
                    {
                        _coordinator?.NetworkSession.JoinRoom(code, "Player_2", (success, msg) =>
                        {
                            if (success)
                            {
                                if (textWaitingRoomCode != null) textWaitingRoomCode.text = $"ROOM CODE: {code}";
                                if (textWaitingStatus != null) textWaitingStatus.text = "Connected! Ready to duel.";
                                if (btnWaitingStartDuel != null) btnWaitingStartDuel.interactable = true;
                                ShowScreen(ScreenState.WaitingRoom);
                            }
                            else
                            {
                                if (textPvPStatus != null) textPvPStatus.text = $"Join error: {msg}";
                            }
                        });
                    }
                });

            CreateButton(panelMatchmaking.transform, "Btn_BackToMenu", "BACK TO MAIN MENU", font, 13, new Color(0.4f, 0.2f, 0.2f), Color.white,
                new Vector2(0.5f, 0.10f), new Vector2(0.5f, 0.10f), Vector2.zero, new Vector2(220f, 40f),
                () => ShowScreen(ScreenState.MainMenu));

            // 11. WAITING ROOM PANEL
            panelWaitingRoom = CreatePanel(root, "Panel_WaitingRoom", new Color(0.04f, 0.05f, 0.1f, 0.96f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelWaitingRoom.SetActive(false);

            CreateText(panelWaitingRoom.transform, "Header_Waiting", "DUEL WAITING ROOM", font, 28, FontStyle.Bold, new Color(1f, 0.85f, 0.2f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), Vector2.zero, new Vector2(600f, 50f));

            textWaitingRoomCode = CreateText(panelWaitingRoom.transform, "Text_WaitingCode", "ROOM CODE: ------", font, 24, FontStyle.Bold, new Color(0.3f, 0.9f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(500f, 40f));
            textRoomCodeDisplay = textWaitingRoomCode;

            textWaitingStatus = CreateText(panelWaitingRoom.transform, "Text_WaitingStatus", "Waiting for opponent to connect...", font, 16, FontStyle.Normal, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(600f, 50f));

            btnWaitingStartDuel = CreateButton(panelWaitingRoom.transform, "Btn_StartDuel", "START DUEL", font, 18, new Color(0.2f, 0.6f, 0.35f), Color.white,
                new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(280f, 50f),
                () =>
                {
                    runner?.EnsureMatchInitialized();
                    _coordinator?.StartBattle(useDebugQuickStart: true);
                    ShowScreen(ScreenState.CombatHUD);
                });

            btnWaitingLeave = CreateButton(panelWaitingRoom.transform, "Btn_LeaveWaiting", "LEAVE ROOM", font, 14, new Color(0.5f, 0.2f, 0.2f), Color.white,
                new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(220f, 40f),
                () =>
                {
                    _coordinator?.NetworkSession.LeaveRoom();
                    ShowScreen(ScreenState.MainMenu);
                });

            // Initial screen presentation
            if (runner != null && runner.startInMainMenu && !runner.runAutomatedVerticalSliceOnStart)
            {
                ShowScreen(ScreenState.MainMenu);
            }
            else
            {
                ShowScreen(ScreenState.CombatHUD);
            }
        }

        public void SetCombatHUDVisible(bool visible)
        {
            if (_playerPanel != null) _playerPanel.SetActive(visible);
            if (_oppPanel != null) _oppPanel.SetActive(visible);
            if (_statusPanel != null) _statusPanel.SetActive(visible);
            if (_centerPanel != null) _centerPanel.SetActive(visible);
            if (_bannerPanel != null) _bannerPanel.SetActive(visible);
            if (_spellPanel != null) _spellPanel.SetActive(visible);
            if (_logPanel != null) _logPanel.SetActive(visible);
            if (_debugPanel != null) _debugPanel.SetActive(visible);
            if (btnActionButton != null) btnActionButton.gameObject.SetActive(visible);
            if (panelDiagnostics != null && !visible) panelDiagnostics.SetActive(false);
        }

        public void ShowScreen(ScreenState screen)
        {
            CurrentScreen = screen;

            if (panelMainMenu != null) panelMainMenu.SetActive(screen == ScreenState.MainMenu);
            if (panelMatchmaking != null) panelMatchmaking.SetActive(screen == ScreenState.Matchmaking);
            if (panelWaitingRoom != null) panelWaitingRoom.SetActive(screen == ScreenState.WaitingRoom);
            if (panelPathSelection != null) panelPathSelection.SetActive(screen == ScreenState.PathSelection);
            if (panelGuildSelection != null) panelGuildSelection.SetActive(screen == ScreenState.GuildSelection);
            if (panelBattleConclusion != null) panelBattleConclusion.SetActive(screen == ScreenState.BattleConclusion);

            bool isCombatVisible = (screen == ScreenState.CombatHUD);
            SetCombatHUDVisible(isCombatVisible);

            if (isCombatVisible)
            {
                RefreshInitialUI();
            }
        }

        public void UpdateDevMetricsOverlay()
        {
            if (panelDevMetrics == null || !panelDevMetrics.activeSelf || textDevMetrics == null) return;

            string roomCode = _coordinator?.NetworkSession?.CurrentRoom?.roomCode ?? "------";
            string localId = _coordinator?.LocalPlayer?.PlayerId ?? "Local";
            string remoteId = _coordinator?.OpponentPlayer?.PlayerId ?? "Remote";
            Vector3 localPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Vector3 remotePos = _coordinator?.ARTracking?.TargetStatus.estimatedPosition ?? Vector3.zero;
            Vector3 sharedPos = (_coordinator?.ARTracking is ARFoundationTrackingService af && af.calibrator != null)
                ? af.calibrator.BattleSpace.LocalToSharedPosition(localPos)
                : localPos;
            string arTracking = _coordinator?.ARTracking?.IsARInitialized == true ? "TRACKING" : "LIMITED";
            string remoteTracking = _coordinator?.ARTracking?.TargetStatus.confidence.ToString() ?? "None";
            float rtt = (_coordinator?.NetworkSession is LocalPvPPrototypeTransport lp) ? lp.NetworkRttMs : 0f;
            string connState = _coordinator?.NetworkSession?.ConnectionState.ToString() ?? "Disconnected";
            string targetId = _coordinator?.OpponentHitbox?.OwnerPlayerId ?? "None";
            float targetDist = _coordinator?.ARTracking?.TargetStatus.distanceMeters ?? 0f;
            string targetConf = _coordinator?.ARTracking?.TargetStatus.confidence.ToString() ?? "None";
            string phase = _coordinator?.StateMachine?.CurrentPhase.ToString() ?? "WaitingForPlayer";

            textDevMetrics.text = $"[14-METRIC DEV OVERLAY]\n" +
                $"ROOM CODE: {roomCode}\n" +
                $"LOCAL PLAYER: {localId}\n" +
                $"REMOTE PLAYER: {remoteId}\n" +
                $"LOCAL POS: {localPos.x:F2}, {localPos.y:F2}, {localPos.z:F2}\n" +
                $"REMOTE POS: {remotePos.x:F2}, {remotePos.y:F2}, {remotePos.z:F2}\n" +
                $"SHARED BATTLE POS: {sharedPos.x:F2}, {sharedPos.y:F2}, {sharedPos.z:F2}\n" +
                $"AR TRACKING: {arTracking}\n" +
                $"REMOTE TRACKING: {remoteTracking}\n" +
                $"NETWORK RTT: {rtt:F1} ms\n" +
                $"CONN STATE: {connState}\n" +
                $"TARGET ID: {targetId}\n" +
                $"TARGET DISTANCE: {targetDist:F2} m\n" +
                $"TARGET CONFIDENCE: {targetConf}\n" +
                $"BATTLE PHASE: {phase}";
        }

        // --- UI Construction Helpers ---

        private GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return go;
        }

        private Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static Sprite _cachedReticleSprite;
        private static Sprite GetOrCreateReticleSprite()
        {
            if (_cachedReticleSprite != null) return _cachedReticleSprite;

            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            float innerR = 46f;
            float outerR = 52f;

            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Circular ring with smooth anti-aliased edge
                    float alpha = 0f;
                    if (dist >= innerR && dist <= outerR)
                    {
                        float edgeDist = Mathf.Min(dist - innerR, outerR - dist);
                        alpha = Mathf.Clamp01(edgeDist / 1.5f);
                    }

                    // 4 Cardinal Ticks (exterior of ring)
                    bool isTickX = Mathf.Abs(dx) <= 1.5f && (Mathf.Abs(dy) >= 54f && Mathf.Abs(dy) <= 62f);
                    bool isTickY = Mathf.Abs(dy) <= 1.5f && (Mathf.Abs(dx) >= 54f && Mathf.Abs(dx) <= 62f);
                    if (isTickX || isTickY)
                    {
                        alpha = 1f;
                    }

                    // Tiny central pip
                    if (dist <= 2f)
                    {
                        alpha = 0.6f;
                    }

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            _cachedReticleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _cachedReticleSprite;
        }

        private Text CreateText(Transform parent, string name, string text, Font font, int size, FontStyle style, Color color, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            var txt = go.GetComponent<Text>();
            txt.text = text;
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private Slider CreateBar(Transform parent, string name, Color fillColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, out Text labelText, string defaultLabel, Font font)
        {
            var barGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
            barGo.transform.SetParent(parent, false);
            var barRect = barGo.GetComponent<RectTransform>();
            barRect.anchorMin = anchorMin;
            barRect.anchorMax = anchorMax;
            barRect.anchoredPosition = anchoredPos;
            barRect.sizeDelta = size;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(barGo.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.9f);

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(barGo.transform, false);
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            // Fill
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillGo.GetComponent<Image>().color = fillColor;

            var slider = barGo.GetComponent<Slider>();
            slider.targetGraphic = bgGo.GetComponent<Image>();
            slider.fillRect = fillRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            // Bar Label
            labelText = CreateText(barGo.transform, "Text_Label", defaultLabel, font, 12, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return slider;
        }

        private Button CreateButton(Transform parent, string name, string label, Font font, int fontSize, Color bgColor, Color textColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Action onClick)
        {
            var btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            var rect = btnGo.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = btnGo.GetComponent<Image>();
            img.color = bgColor;

            var btn = btnGo.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.3f;
            colors.pressedColor = bgColor * 0.7f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            CreateText(btnGo.transform, "Text_Label", label, font, fontSize, FontStyle.Bold, textColor,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return btn;
        }

        private InputField CreateInputField(Transform parent, string name, string placeholderText, Font font, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);

            var textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(go.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-20f, 0f);

            var textComp = textObj.GetComponent<Text>();
            textComp.font = font;
            textComp.fontSize = fontSize;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.MiddleCenter;

            var placeObj = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeObj.transform.SetParent(go.transform, false);
            var placeRect = placeObj.GetComponent<RectTransform>();
            placeRect.anchorMin = Vector2.zero;
            placeRect.anchorMax = Vector2.one;
            placeRect.sizeDelta = new Vector2(-20f, 0f);

            var placeComp = placeObj.GetComponent<Text>();
            placeComp.font = font;
            placeComp.fontSize = fontSize;
            placeComp.fontStyle = FontStyle.Italic;
            placeComp.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            placeComp.text = placeholderText;
            placeComp.alignment = TextAnchor.MiddleCenter;

            var inputField = go.GetComponent<InputField>();
            inputField.textComponent = textComp;
            inputField.placeholder = placeComp;
            return inputField;
        }
    }
}
