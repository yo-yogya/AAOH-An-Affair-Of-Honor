using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Battle;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Environment;
using ArGuildEncounter.Networking;
using ArGuildEncounter.AR;
using ArGuildEncounter.Voice;
using ArGuildEncounter.Gesture;
using ArGuildEncounter.UI;
using ArGuildEncounter.CPU;
using ArGuildEncounter.Players;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Master in-scene runner component for the AR Guild Encounter foundation.
    /// Can be added to a scene to initialize the BattleCoordinator, bind the HUD,
    /// and optionally run the automated vertical slice test suite.
    /// </summary>
    public class GameFoundationRunner : MonoBehaviour
    {
        [Header("Configuration")]
        public CombatConfig combatConfig;
        public BattleHUDController hudController;

        [Header("Automated Test Mode")]
        public bool startInMainMenu = false;
        public bool runAutomatedVerticalSliceOnStart = false;

        [Header("Authoritative Player Model Prefab")]
        public GameObject playerCharacterPrefab;
        public PlayerCharacter localCharacter;
        public PlayerCharacter remoteCharacter;

        [Header("Two-Device AR PvP Configuration")]
        public bool useRealNetworking = false;
        public bool useRealAR = false;
        public PlayerHitbox localHitbox;
        public PlayerHitbox remoteHitbox;
        public RemotePlayerVisual remoteVisual;
        public SpatialInterpolator remoteInterpolator;
        public LocalPvPPrototypeTransport realNetworkTransport;
        public ARFoundationTrackingService realARService;

        [Header("Voice Provider")]
        [Header("Voice Provider")]
        [Tooltip("When true, uses real voice speech recognition (LiveKit / Windows Speech) instead of mock voice.")]
        public bool useLiveKitVoice = true;
        public LiveKitVoiceInputService liveKitVoiceService;

        [Header("Hand-Tracking Gesture Provider")]
        [Tooltip("When true, uses real HandTrackingGestureService for somatic gestures.")]
        public bool useHandTracking = true;
        public ArGuildEncounter.Gesture.HandTrackingGestureService handTrackingService;
        private ArGuildEncounter.Gesture.HandTrackingGestureService _handTrackingService;

        private BattleCoordinator _coordinator;
        private INetworkSessionProvider _network;
        private IARTrackingService _ar;
        private IVoiceInputService _voice;
        private IGestureService _gesture;
        private bool _isRunningAutomatedSlice = false;

        public BattleCoordinator Coordinator => _coordinator;
        public ArGuildEncounter.Gesture.HandTrackingGestureService HandTracking => _handTrackingService;

        private void Awake()
        {
            if (hudController == null)
            {
                hudController = FindAnyObjectByType<BattleHUDController>();
            }

            // Ensure real voice model is enabled
            useLiveKitVoice = true;

            InitializeServices();
        }

        private void Start()
        {
            if (runAutomatedVerticalSliceOnStart)
            {
                StartCoroutine(RunAutomatedVerticalSliceCoroutine());
            }
            else if (!startInMainMenu)
            {
                LaunchVsCpu();
            }
            else
            {
                hudController?.ShowScreen(ScreenState.MainMenu);
            }
        }

        private void Update()
        {
            if (_coordinator != null)
            {
                _coordinator.TickCastTimer(Time.deltaTime);

                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && _coordinator.StateMachine.CurrentPhase == BattlePhase.GestureWindow)
                {
                    if (keyboard.uKey.wasPressedThisFrame)
                    {
                        TestHandsUp();
                        hudController?.AppendLog("[HOTKEY 'U'] Hands-Up (GESTURE_THRUST) triggered!");
                    }
                    else if (keyboard.tKey.wasPressedThisFrame)
                    {
                        TestGestureTimeout();
                        hudController?.AppendLog("[HOTKEY 'T'] Gesture Timeout triggered!");
                    }
                    else if (keyboard.fKey.wasPressedThisFrame)
                    {
                        TestGestureFail();
                        hudController?.AppendLog("[HOTKEY 'F'] Gesture Failure triggered!");
                    }
                }
            }
        }

        public void InitializeServices()
        {
            if (_coordinator != null) return;

            if (combatConfig == null)
            {
                combatConfig = TestCombatDataFactory.CreateTestCombatConfig();
            }
            combatConfig.firstPlayerRule = FirstPlayerRule.HostFirst;

#if UNITY_ANDROID && !UNITY_EDITOR
            useRealNetworking = true;
            useRealAR = true;
            useLiveKitVoice = true;
#endif

            if (useRealNetworking)
            {
                realNetworkTransport = FindAnyObjectByType<LocalPvPPrototypeTransport>() ?? gameObject.AddComponent<LocalPvPPrototypeTransport>();
                _network = realNetworkTransport;
            }
            else
            {
                _network = new MockNetworkSessionProvider();
            }

            if (useRealAR)
            {
                realARService = FindAnyObjectByType<ARFoundationTrackingService>() ?? gameObject.AddComponent<ARFoundationTrackingService>();
                _ar = realARService;
            }
            else
            {
                _ar = new MockARTrackingService();
            }

            // Voice provider: LiveKit/Seeway for production, Mock for editor/testing
            if (useLiveKitVoice)
            {
                liveKitVoiceService = FindAnyObjectByType<LiveKitVoiceInputService>() ?? gameObject.AddComponent<LiveKitVoiceInputService>();
                _voice = liveKitVoiceService;
            }
            else
            {
                _voice = new MockVoiceInputService();
            }

            // Gesture provider: HandTrackingGestureService (Hands-Up -> GESTURE_THRUST) or Mock
            if (useHandTracking)
            {
                _handTrackingService = handTrackingService ?? FindAnyObjectByType<ArGuildEncounter.Gesture.HandTrackingGestureService>() ?? gameObject.AddComponent<ArGuildEncounter.Gesture.HandTrackingGestureService>();
                _handTrackingService.EnableWebcamCapture = true;
                _handTrackingService.Initialize(() => Time.time, 0.70f);
                _handTrackingService.StartWebcam();
                _gesture = _handTrackingService;
            }
            else
            {
                _gesture = new MockGestureService();
            }

            _coordinator = new BattleCoordinator(combatConfig, _network, _ar, _voice, _gesture, () => Time.time);
            _coordinator.StateMachine.PhaseChanged += OnCoordinatorPhaseChanged;
            _coordinator.OnCpuTurnRequested += () => StartCoroutine(ExecuteCpuTurnCoroutine());

            try
            {
                var arena = FindAnyObjectByType<BattleArenaController>();
                if (arena == null && gameObject != null)
                {
                    gameObject.AddComponent<BattleArenaController>();
                }

                var cpu = FindAnyObjectByType<CPUController>();
                if (cpu == null && gameObject != null)
                {
                    cpu = gameObject.AddComponent<CPUController>();
                }
                if (cpu != null)
                {
                    cpu.Bind(_coordinator);
                }

                var vfx = FindAnyObjectByType<CombatVFXManager>();
                if (vfx == null && gameObject != null)
                {
                    vfx = gameObject.AddComponent<CombatVFXManager>();
                }
                if (vfx != null)
                {
                    _coordinator.OnAttackPresentationRequested += (result) =>
                    {
                        bool isHost = _coordinator.TurnManager != null && _coordinator.TurnManager.IsHostTurn;
                        Vector3 casterPos = isHost ? Vector3.zero : new Vector3(0, 0, 3.5f);
                        Vector3 targetPos = isHost ? new Vector3(0, 0, 3.5f) : Vector3.zero;
                        vfx.PlayAttackSequence(casterPos, targetPos, _coordinator.SelectedSpell, result);
                    };
                }
            }
            catch { }

            EnsureDualHitboxesAndVisuals();

            if (hudController != null)
            {
                hudController.runner = this;
                hudController.BindCoordinator(_coordinator);
            }
        }

        public void EnsureDualHitboxesAndVisuals()
        {
            // 0. Ensure authoritative PlayerCharacter prefab reference
            if (playerCharacterPrefab == null)
            {
                playerCharacterPrefab = Resources.Load<GameObject>("PlayerCharacter_Prefab");
#if UNITY_EDITOR
                if (playerCharacterPrefab == null)
                {
                    playerCharacterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerCharacter_Prefab.prefab");
                }
#endif
            }

            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");

            // 0b. Frame Camera for AR Duel
            if (Camera.main != null)
            {
                var camObj = Camera.main;
                if (!useRealAR)
                {
                    var driver = camObj.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                    if (driver != null) driver.enabled = false;
                }

                var camParent = camObj.transform.parent;
                var originTransform = camParent != null && camParent.parent != null ? camParent.parent : (camParent != null ? camParent : camObj.transform);
                originTransform.position = new Vector3(0f, 1.65f, -2.20f);
                originTransform.rotation = Quaternion.Euler(25f, 0f, 0f);
                if (camParent != null && camParent != camObj.transform)
                {
                    camParent.localPosition = Vector3.zero;
                    camParent.localRotation = Quaternion.identity;
                }
                camObj.transform.localPosition = Vector3.zero;
                camObj.transform.localRotation = Quaternion.identity;
            }

            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : new Vector3(0f, 1.65f, -2.20f);
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;

            // Screen center crosshair ray (0.5, 0.5)
            Ray crosshairRay = cam != null
                ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(camPos, camFwd);

            // Valid ground target point (raycast against ground colliders or plane Y = 0)
            Vector3 groundTarget;
            if (Physics.Raycast(crosshairRay, out RaycastHit hit, 30f))
            {
                groundTarget = hit.point;
            }
            else
            {
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(crosshairRay, out float enterDist))
                {
                    groundTarget = crosshairRay.GetPoint(enterDist);
                }
                else
                {
                    groundTarget = new Vector3(camPos.x + camFwd.x * 2.5f, 0f, camPos.z + camFwd.z * 2.5f);
                }
            }
            groundTarget.y = 0f; // Character feet strictly on AR ground plane

            Vector3 toCam = camPos - groundTarget;
            toCam.y = 0f;
            Quaternion spawnRot = toCam.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toCam.normalized, Vector3.up)
                : Quaternion.Euler(0f, 180f, 0f);

            // 1. Local Player Entity (Dark Necromancer standing grounded at crosshair target)
            if (localCharacter == null)
            {
                var existingLocal = GameObject.Find("PlayerCharacter_Local");
                if (existingLocal != null) localCharacter = existingLocal.GetComponent<PlayerCharacter>();
            }

            if (localCharacter == null && playerCharacterPrefab != null)
            {
                var go = Instantiate(playerCharacterPrefab, groundTarget, spawnRot);
                go.name = "PlayerCharacter_Local";
                localCharacter = go.GetComponent<PlayerCharacter>();
            }

            if (localCharacter != null)
            {
                localCharacter.transform.position = groundTarget;
                localCharacter.transform.rotation = spawnRot;
                localCharacter.gameObject.SetActive(true);
                localCharacter.Initialize("Player_1", isLocal: true, isHost: true, magma);
                localHitbox = localCharacter.PlayerHitbox;
            }

            if (localHitbox == null)
            {
                var existingHitbox = GameObject.Find("PlayerHitbox_Local");
                if (existingHitbox != null) localHitbox = existingHitbox.GetComponent<PlayerHitbox>();
                else
                {
                    var localGo = new GameObject("PlayerHitbox_Local");
                    localGo.transform.position = groundTarget;
                    localHitbox = localGo.AddComponent<PlayerHitbox>();
                }
            }

            localHitbox.OwnerPlayerId = "Player_1";
            localHitbox.IsLocalPlayer = true;
            localHitbox.showDebugHitbox = false;
            localHitbox.UpdateColliderConfiguration();

            // 2. Remote / Opponent Player Entity
            Vector3 remotePos = groundTarget + Vector3.forward * 2.2f;
            remotePos.y = 0f;

            if (remoteCharacter == null)
            {
                var existingRemote = GameObject.Find("PlayerCharacter_Remote");
                if (existingRemote != null) remoteCharacter = existingRemote.GetComponent<PlayerCharacter>();
            }

            if (remoteCharacter == null && playerCharacterPrefab != null)
            {
                var go = Instantiate(playerCharacterPrefab, remotePos, Quaternion.Euler(0f, 180f, 0f));
                go.name = "PlayerCharacter_Remote";
                remoteCharacter = go.GetComponent<PlayerCharacter>();
            }

            if (remoteCharacter != null)
            {
                remoteCharacter.transform.position = remotePos;
                remoteCharacter.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                remoteCharacter.gameObject.SetActive(true);
                remoteCharacter.Initialize("Player_2", isLocal: false, isHost: false, aqua);
                remoteHitbox = remoteCharacter.PlayerHitbox;
            }

            if (remoteHitbox == null)
            {
                var existingHitbox = GameObject.Find("PlayerHitbox_Remote");
                if (existingHitbox != null) remoteHitbox = existingHitbox.GetComponent<PlayerHitbox>();
                else
                {
                    var remoteGo = new GameObject("PlayerHitbox_Remote");
                    remoteGo.transform.position = remotePos;
                    remoteHitbox = remoteGo.AddComponent<PlayerHitbox>();
                }
            }

            remoteHitbox.OwnerPlayerId = "Player_2";
            remoteHitbox.IsLocalPlayer = false;
            remoteHitbox.showDebugHitbox = false;
            remoteHitbox.SetTrackingState(true, ARTrackingConfidence.High);

            // 3. Section 19 Diagnostic Logging
            var modelRenderer = localCharacter != null ? localCharacter.GetComponentInChildren<Renderer>() : null;
            var modelTransform = modelRenderer != null ? modelRenderer.transform : (localCharacter != null ? localCharacter.transform : null);
            Bounds modelBounds = modelRenderer != null ? modelRenderer.bounds : new Bounds();

            Debug.Log($"[PLAYER SPAWN AT CROSSHAIR DIAGNOSTIC]\n" +
                $"Player world position: {(localCharacter != null ? localCharacter.transform.position.ToString("F3") : "NULL")}\n" +
                $"Player local position: {(localCharacter != null ? localCharacter.transform.localPosition.ToString("F3") : "NULL")}\n" +
                $"Camera world position: {camPos.ToString("F3")}\n" +
                $"Crosshair ray origin: {crosshairRay.origin.ToString("F3")}\n" +
                $"Crosshair ray direction: {crosshairRay.direction.ToString("F3")}\n" +
                $"Raycast hit position: {groundTarget.ToString("F3")}\n" +
                $"Spawn position: {groundTarget.ToString("F3")}\n" +
                $"Model world scale: {(modelTransform != null ? modelTransform.lossyScale.ToString("F4") : "NULL")}\n" +
                $"Model world bounds: center={modelBounds.center.ToString("F3")}, size={modelBounds.size.ToString("F3")}\n" +
                $"Model renderer enabled: {(modelRenderer != null && modelRenderer.enabled)}\n" +
                $"Model active: {(modelTransform != null && modelTransform.gameObject.activeInHierarchy)}\n" +
                $"Distance from camera: {(localCharacter != null ? Vector3.Distance(camPos, localCharacter.transform.position) : 0f):F2}m");

            // 3. Spatial Interpolator (Smooths remote movement)
            if (remoteInterpolator == null && remoteCharacter != null)
            {
                remoteInterpolator = remoteCharacter.GetComponent<SpatialInterpolator>() ?? remoteCharacter.gameObject.AddComponent<SpatialInterpolator>();
                remoteInterpolator.visualTransform = remoteCharacter.CharacterViewRoot;
                remoteInterpolator.hitboxTransform = remoteHitbox.transform;
            }

            // 4. Clean up any obsolete standalone Opponent_Target_Hitbox in the scene
            var obsoleteHitbox = GameObject.Find("Opponent_Target_Hitbox");
            if (obsoleteHitbox != null && (remoteCharacter != null || remoteHitbox != null))
            {
                obsoleteHitbox.SetActive(false);
            }

            // 5. Bind Dual Hitboxes to Coordinator
            if (_coordinator != null)
            {
                _coordinator.BindDualHitboxes(localHitbox, remoteHitbox);
            }
        }

        public void EnsureMatchInitialized()
        {
            if (_coordinator == null)
            {
                InitializeServices();
            }

            if (_coordinator.LocalPlayer == null)
            {
                var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
                var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
                var terrain = AuthoritativeCombatRoster.GetTerrain("VOLCANIC_RUINS");
                var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Volcanic Ruins", null);

                var spellsPlayer1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
                var spellsPlayer2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");

                _coordinator.SetupMatch("Player_1", magma, spellsPlayer1, "Player_2", aqua, spellsPlayer2, arena, terrain);
                _coordinator.StartBattle(useDebugQuickStart: true);
            }
        }

        public IEnumerator RunAutomatedVerticalSliceCoroutine()
        {
            Debug.Log("[GameFoundationRunner] Launching Visible Automated Vertical Slice...");

            if (hudController == null)
            {
                hudController = FindAnyObjectByType<BattleHUDController>();
            }

            if (hudController == null)
            {
                Debug.LogError("[GameFoundationRunner] Aborted: BattleHUDController component not found.");
                yield break;
            }

            if (_coordinator == null)
            {
                InitializeServices();
            }

            if (_coordinator == null)
            {
                Debug.LogError("[GameFoundationRunner] Aborted: BattleCoordinator failed to initialize.");
                yield break;
            }

            _isRunningAutomatedSlice = true;
            hudController.AppendLog("[RUNNER] Starting visible vertical slice simulation (COMBAT-BALANCE-V1)...");
            yield return new WaitForSeconds(0.4f);

            // Step 1: Create Room
            string roomCode = "";
            bool step1Failed = false;
            string step1Error = "";
            _network.CreateRoom("Player_1", (success, code) =>
            {
                if (!success || string.IsNullOrEmpty(code))
                {
                    step1Failed = true;
                    step1Error = "Room creation failed in mock network.";
                    return;
                }
                roomCode = code;
                if (!_coordinator.StateMachine.TryTransition(BattlePhase.RoomCreated, $"Room code: {code}"))
                {
                    step1Failed = true;
                    step1Error = "State machine refused transition to RoomCreated.";
                }
            });

            if (step1Failed)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted at Step 1: {step1Error}");
                yield break;
            }

            hudController.AppendLog($"[STEP 1] Room Created. 6-Digit Code: {roomCode}");
            yield return new WaitForSeconds(0.5f);

            // Step 2 & 3: Join Room & Connect
            bool step2Failed = false;
            string step2Error = "";
            _network.JoinRoom(roomCode, "Player_2", (success, msg) =>
            {
                if (!success)
                {
                    step2Failed = true;
                    step2Error = $"Join room failed: {msg}";
                    return;
                }
                if (!_coordinator.StateMachine.TryTransition(BattlePhase.PlayerJoined, "Guest connected"))
                {
                    step2Failed = true;
                    step2Error = "State machine refused transition to PlayerJoined.";
                }
            });

            if (step2Failed)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted at Step 2/3: {step2Error}");
                yield break;
            }

            hudController.AppendLog("[STEP 2 & 3] Player 2 joined. Guest connected.");
            yield return new WaitForSeconds(0.5f);

            // Step 4: Match Setup & Debug Quick-Start to BattleReady
            EnsureMatchInitialized();
            if (_coordinator.LocalPlayer == null || _coordinator.OpponentPlayer == null)
            {
                Debug.LogError("[GameFoundationRunner] Vertical Slice Aborted at Step 4: Local or Opponent player is null after setup.");
                yield break;
            }

            if (!_coordinator.StateMachine.IsInBattleLoop)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted at Step 4: Expected to be in battle loop, but phase is {_coordinator.StateMachine.CurrentPhase}.");
                yield break;
            }

            hudController.AppendLog("[STEP 4] Match setup complete (Magma vs Aqua on Volcanic Ruins). Initialized Turn 1.");
            yield return new WaitForSeconds(0.5f);

            // --- Condition Assertions Before Step 5 ---
            if (_coordinator.StateMachine.CurrentPhase != BattlePhase.SpellSelection)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted before Step 5: Expected phase SpellSelection, got {_coordinator.StateMachine.CurrentPhase}.");
                yield break;
            }

            var activePlayer = _coordinator.TurnManager.GetActivePlayerState();
            if (activePlayer == null || activePlayer.PlayerId != _coordinator.LocalPlayer.PlayerId)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted before Step 5: Expected LocalPlayer ({_coordinator.LocalPlayer.PlayerId}) active, but got {activePlayer?.PlayerId ?? "null"}.");
                yield break;
            }

            if (_coordinator.SelectedSpell != null)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted before Step 5: Expected SelectedSpell to be null, got {_coordinator.SelectedSpell.displayName}.");
                yield break;
            }

            // Step 5: Voice Spell Selection ("Fireball")
            string targetSpellName = _coordinator.LocalPlayer.SpellSlots[0].displayName; // "Fireball"
            hudController.AppendLog($"[STEP 5] Voice Command: '{targetSpellName}' summoned.");
            _voice.SimulateSpokenPhrase(targetSpellName, null);

            if (_coordinator.SelectedSpell == null || _coordinator.StateMachine.CurrentPhase != BattlePhase.GestureWindow)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted at Step 5: Expected GestureWindow with selected spell, got phase {_coordinator.StateMachine.CurrentPhase} and spell {_coordinator.SelectedSpell?.displayName ?? "null"}.");
                yield break;
            }

            if (_coordinator.SelectedSpell.damageClass != DamageClass.L || _coordinator.SelectedSpell.manaCost != 12)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted at Step 5: Selected spell stats mismatch. Expected L / 12 Mana, got {_coordinator.SelectedSpell.damageClass} / {_coordinator.SelectedSpell.manaCost} Mana.");
                yield break;
            }
            yield return new WaitForSeconds(0.5f);

            // --- Condition Assertions Before Step 6 ---
            if (_coordinator.StateMachine.CurrentPhase != BattlePhase.GestureWindow || _coordinator.SelectedSpell == null)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted before Step 6: Expected GestureWindow with non-null spell, got phase {_coordinator.StateMachine.CurrentPhase}.");
                yield break;
            }

            // Step 6: Gesture Validation (reading directly from SelectedSpell.gestureId)
            string expectedGesture = _coordinator.SelectedSpell.gestureId;
            hudController.AppendLog($"[STEP 6] Gesture '{expectedGesture}' confirmed (95% confidence).");
            _gesture.SimulateGesturePerformed(expectedGesture, 0.95f);
            if (_coordinator.StateMachine.CurrentPhase != BattlePhase.AimPhase)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted at Step 6: Expected AimPhase after gesture, got phase {_coordinator.StateMachine.CurrentPhase}.");
                yield break;
            }
            yield return new WaitForSeconds(0.5f);

            // --- Condition Assertions Before Step 7 ---
            if (_coordinator.StateMachine.CurrentPhase != BattlePhase.AimPhase)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted before Step 7: Expected AimPhase, got phase {_coordinator.StateMachine.CurrentPhase}.");
                yield break;
            }

            // Step 7: AR Opponent Target Lock
            hudController.AppendLog("[STEP 7] AR Opponent locked at 4.5m distance.");
            _ar.SimulateTargetAcquisition(new Vector3(0, 0, 4.5f), 4.5f);
            var hitbox = FindAnyObjectByType<OpponentHitbox>();
            if (hitbox != null) hitbox.SetTargetLocked(true);

            if (!_ar.TargetStatus.isHitboxAimed)
            {
                Debug.LogError("[GameFoundationRunner] Vertical Slice Aborted at Step 7: Target acquisition failed.");
                yield break;
            }
            yield return new WaitForSeconds(0.5f);

            // --- Condition Assertions Before Step 8 ---
            if (_coordinator.StateMachine.CurrentPhase != BattlePhase.AimPhase || !_ar.TargetStatus.isHitboxAimed)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted before Step 8: Expected AimPhase and locked target.");
                yield break;
            }

            // Step 8: Commit Attack Launch & Resolution
            hudController.AppendLog("[STEP 8] Committing Attack Launch at opponent hitbox...");
            _coordinator.CommitAttackLaunch();
            yield return new WaitForSeconds(0.5f);

            if (_coordinator.StateMachine.CurrentPhase == BattlePhase.WaitingForPlayer)
            {
                Debug.LogError("[GameFoundationRunner] Vertical Slice Aborted at Step 8: Coordinator reverted to WaitingForPlayer unexpectedly.");
                yield break;
            }

            if (!_coordinator.LastAttackResult.isCastSuccessful)
            {
                Debug.LogError("[GameFoundationRunner] Vertical Slice Aborted at Step 8: Attack resolution failed cast validation.");
                yield break;
            }

            // Step 9: Verify turn advance, authoritative miss chance calculation, and mana regeneration
            hudController.AppendLog("[STEP 9] Verifying authoritative resolution, damage, and turn advance...");
            if (_coordinator.TurnManager.CurrentTurnNumber < 2)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted at Step 9: Expected Turn >= 2 after resolution, got {_coordinator.TurnManager.CurrentTurnNumber}.");
                yield break;
            }

            // Verify Authoritative Base Miss Chance calculation is never 0.0%
            if (_coordinator.LastAttackResult.finalMissChance < 0.075f)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted at Step 9: Authoritative finalMissChance was {_coordinator.LastAttackResult.finalMissChance * 100f}%, which is below global base miss (7.5%).");
                yield break;
            }

            // Mana check: 100 max - 12 (Fireball) + 10 (End-of-turn regen) = 98
            if (_coordinator.LocalPlayer.CurrentMana != 98)
            {
                Debug.LogError($"[GameFoundationRunner] Vertical Slice Aborted at Step 9: Expected LocalPlayer Mana 98 (100 - 12 + 10), got {_coordinator.LocalPlayer.CurrentMana}.");
                yield break;
            }

            hudController.AppendLog($"[SUCCESS] Fireball resolved! Dealt {_coordinator.LastAttackResult.finalDamageDealt} DMG. Mana: {_coordinator.LocalPlayer.CurrentMana}/100.");
            hudController.AppendLog("[VERTICAL SLICE COMPLETE] 12-step vertical slice visibly executed on live coordinator!");
            Debug.Log("[GameFoundationRunner] Visible Automated Vertical Slice completed successfully!");
            _isRunningAutomatedSlice = false;
        }

        // =========================================================================
        // 16 STATE-AWARE DEVELOPER CONTROLS HOOKS
        // =========================================================================

        [ContextMenu("1. Simulate Voice Selection")]
        public void TestVoiceSelection()
        {
            EnsureMatchInitialized();
            SimulateVoiceForActivePlayer();
        }

        public void SimulateVoiceForActivePlayer(string spellName = null)
        {
            EnsureMatchInitialized();
            if (!string.IsNullOrEmpty(spellName))
            {
                _voice.SimulateSpokenPhrase(spellName, null);
                return;
            }

            var activePlayer = _coordinator.TurnManager?.GetActivePlayerState() ?? _coordinator.LocalPlayer;
            if (activePlayer != null && activePlayer.SpellSlots != null && activePlayer.SpellSlots.Length > 0 && activePlayer.SpellSlots[0] != null)
            {
                _voice.SimulateSpokenPhrase(activePlayer.SpellSlots[0].displayName, null);
            }
            else
            {
                _voice.SimulateSpokenPhrase("Fireball", null);
            }
        }

        [ContextMenu("2. Simulate Voice Unrecognized")]
        public void TestVoiceUnrecognized()
        {
            EnsureMatchInitialized();
            _voice.SimulateSpokenPhrase("Abracadabra", null);
        }

        [ContextMenu("3. Simulate Tap Slot 1")]
        public void TestTapSlot1()
        {
            EnsureMatchInitialized();
            _coordinator.SelectSpellManualFallback(0);
        }

        [ContextMenu("4. Simulate Gesture Success")]
        public void TestGestureSuccess()
        {
            EnsureMatchInitialized();
            SimulateCurrentSpellGesture(0.95f);
        }

        [ContextMenu("4b. Simulate Hands Up (Hand Tracking)")]
        public void TestHandsUp()
        {
            EnsureMatchInitialized();
            if (_handTrackingService != null)
            {
                _handTrackingService.SimulateHandsUp(0.95f);
            }
            else
            {
                _gesture?.SimulateGesturePerformed(ArGuildEncounter.Gesture.HandsUpClassifier.CanonicalGestureId, 0.95f);
            }
        }

        public void SimulateCurrentSpellGesture(float confidence = 0.95f)
        {
            EnsureMatchInitialized();
            if (_handTrackingService != null)
            {
                _handTrackingService.SimulateHandsUp(confidence);
            }
            else
            {
                string gesture = _coordinator?.SelectedSpell != null ? _coordinator.SelectedSpell.gestureId : ArGuildEncounter.Gesture.HandsUpClassifier.CanonicalGestureId;
                _gesture?.SimulateGesturePerformed(gesture, confidence);
            }
        }

        [ContextMenu("5. Simulate Gesture Fail")]
        public void TestGestureFail()
        {
            EnsureMatchInitialized();
            _gesture?.SimulateGestureFailure();
        }

        [ContextMenu("6. Simulate Gesture Timeout")]
        public void TestGestureTimeout()
        {
            EnsureMatchInitialized();
            _gesture?.SimulateTimeout();
        }

        [ContextMenu("7. Simulate Target Acquired")]
        public void TestTargetAcquired()
        {
            EnsureMatchInitialized();
            _ar.SimulateTargetAcquisition(new Vector3(0, 0, 4.5f), 4.5f);
            if (remoteHitbox != null) remoteHitbox.SetTargetLocked(true);
            var hitbox = FindAnyObjectByType<OpponentHitbox>();
            if (hitbox != null) hitbox.SetTargetLocked(true);
        }

        [ContextMenu("8. Simulate Target Lost")]
        public void TestTargetLost()
        {
            EnsureMatchInitialized();
            _ar.SimulateTargetLost();
            if (remoteHitbox != null) remoteHitbox.SetTargetLocked(false);
            var hitbox = FindAnyObjectByType<OpponentHitbox>();
            if (hitbox != null) hitbox.SetTargetLocked(false);
        }

        [ContextMenu("9. Simulate Attack Launch")]
        public void TestAttackLaunch()
        {
            EnsureMatchInitialized();
            if (_ar != null)
            {
                _ar.SimulateTargetAcquisition(new Vector3(0, 0, 3.5f), 3.5f);
                _ar.RaycastAimAtHitbox(Vector2.zero);
            }
            if (remoteHitbox != null) remoteHitbox.SetTargetLocked(true);
            _coordinator.CommitAttackLaunch();
        }

        [ContextMenu("12. Simulate Disconnect")]
        public void TestDisconnect()
        {
            _network.SimulateTemporaryDrop();
        }

        [ContextMenu("13. Simulate Reconnect")]
        public void TestReconnect()
        {
            _network.SimulateReconnect();
        }

        [ContextMenu("14. Simulate Teleport / Forfeit")]
        public void TestTeleport()
        {
            _network.SimulatePermanentAbandon();
        }

        [ContextMenu("15. Restart Match (Quick-Start)")]
        public void RestartMatchQuickStart()
        {
            if (_coordinator != null)
            {
                _coordinator.ResetMatch();
            }
            EnsureMatchInitialized();
            if (hudController != null)
            {
                hudController.AppendLog("[RUNNER] Match restarted via Debug Quick-Start.");
                hudController.RefreshInitialUI();
            }
        }

        public void LaunchVsCpu()
        {
            EnsureMatchInitialized();
            if (_coordinator != null)
            {
                _coordinator.IsCpuOpponentEnabled = true;
                var cpu = FindAnyObjectByType<CPUController>();
                if (cpu != null)
                {
                    cpu.enabled = true;
                    cpu.Bind(_coordinator);
                }

                // Ensure AR tracking and target lock are established for the CPU duel
                if (_ar is MockARTrackingService mockAr)
                {
                    mockAr.SimulateTargetAcquisition(new Vector3(0f, 0f, 3.6f), 3.6f);
                }
                if (remoteHitbox != null)
                {
                    remoteHitbox.SetTargetLocked(true);
                    remoteHitbox.SetTrackingState(true, ARTrackingConfidence.High);
                }
                var oppHitbox = FindAnyObjectByType<OpponentHitbox>();
                if (oppHitbox != null)
                {
                    oppHitbox.SetTargetLocked(true);
                }

                _coordinator.StartBattle(useDebugQuickStart: true);
            }
            if (hudController != null)
            {
                hudController.ShowScreen(ScreenState.CombatHUD);
                hudController.AppendLog("[RUNNER] Launched VS CPU Duel (Magma vs Aqua).");
                hudController.RefreshInitialUI();
            }
        }

        public void LaunchPvP()
        {
            if (hudController != null)
            {
                hudController.ShowScreen(ScreenState.Matchmaking);
            }
        }

        [ContextMenu("Run Path Selection Verification")]
        public void RunPathSelectionVerification()
        {
            bool success = PathSelectionVerification.RunVerification(msg =>
            {
                Debug.Log(msg);
                hudController?.AppendLog(msg);
            });
            Debug.Log($"[RUNNER] PathSelectionVerification Result: {(success ? "SUCCESS" : "FAILED")}");
        }

        [ContextMenu("16. Reset Battle")]
        public void ResetMatch()
        {
            if (_coordinator != null)
            {
                _coordinator.IsCpuOpponentEnabled = false;
                _coordinator.ResetMatch();
            }
            if (_network != null && _network.CurrentRoom != null)
            {
                _network.LeaveRoom();
            }
            if (_ar != null)
            {
                _ar.SimulateTargetLost();
            }
            if (remoteHitbox != null) remoteHitbox.SetTargetLocked(false);
            var hitbox = FindAnyObjectByType<OpponentHitbox>();
            if (hitbox != null) hitbox.SetTargetLocked(false);

            var cpu = FindAnyObjectByType<CPUController>();
            if (cpu != null)
            {
                cpu.StopAllCoroutines();
            }

            if (hudController != null)
            {
                hudController.ShowScreen(ScreenState.MainMenu);
                hudController.AppendLog("[RUNNER] Match state reset. Returned to Main Menu.");
            }
        }

        private int _statusCycleIndex = 0;
        private static readonly string[] StatusCycle = new[] { "Burn", "Chill", "Shock", "Blind" };

        public void TestCreateRoom()
        {
            _network.CreateRoom("Player_1", (success, code) =>
            {
                if (success)
                {
                    _coordinator?.StateMachine.TryTransition(BattlePhase.RoomCreated, $"Room code: {code}");
                    hudController?.AppendLog($"[DEBUG] Room Created: {code}");
                }
            });
        }

        public void TestJoinRoom(string code = "123456")
        {
            _network.JoinRoom(code, "Player_2", (success, msg) =>
            {
                if (success)
                {
                    _coordinator?.StateMachine.TryTransition(BattlePhase.PlayerJoined, "Guest connected");
                    hudController?.AppendLog("[DEBUG] Player 2 Joined.");
                }
            });
        }

        public void TestStartBattle()
        {
            EnsureMatchInitialized();
            _coordinator?.StartBattle(useDebugQuickStart: false);
        }

        public void TestApplyStatus()
        {
            EnsureMatchInitialized();
            string status = StatusCycle[_statusCycleIndex % StatusCycle.Length];
            _statusCycleIndex++;
            _coordinator?.ApplyStatusDebug(status, targetOpponent: true);
            hudController?.AppendLog($"[DEBUG] Injected {status} on opponent.");
        }

        public void ToggleCpuOpponent()
        {
            if (_coordinator != null)
            {
                _coordinator.IsCpuOpponentEnabled = !_coordinator.IsCpuOpponentEnabled;
                hudController?.AppendLog($"[DEBUG] CPU Opponent Mode: {(_coordinator.IsCpuOpponentEnabled ? "ENABLED" : "DISABLED")}");
                if (_coordinator.IsCpuOpponentEnabled &&
                    _coordinator.TurnManager?.GetActivePlayerState()?.PlayerId == _coordinator.OpponentPlayer?.PlayerId)
                {
                    StartCoroutine(ExecuteCpuTurnCoroutine());
                }
            }
        }

        public IEnumerator ExecuteCpuTurnCoroutine()
        {
            if (_coordinator == null || !_coordinator.IsCpuOpponentEnabled) yield break;

            var cpuComponent = FindAnyObjectByType<CPUController>();
            if (cpuComponent != null && cpuComponent.isActiveAndEnabled) yield break;

            yield return new WaitForSeconds(0.6f);

            var opponent = _coordinator.OpponentPlayer;
            if (opponent == null || _coordinator.TurnManager.ActivePlayerId != opponent.PlayerId) yield break;

            int chosenSlot = -1;
            int bestDamage = -1;

            if (!opponent.HasForceFallback)
            {
                for (int i = 0; i < 5 && i < opponent.SpellSlots.Length; i++)
                {
                    var sp = opponent.SpellSlots[i];
                    if (sp != null && opponent.CanCastSpell(i, out _))
                    {
                        if (sp.baseDamage > bestDamage)
                        {
                            bestDamage = sp.baseDamage;
                            chosenSlot = i;
                        }
                    }
                }
            }

            if (chosenSlot < 0)
            {
                chosenSlot = 5; // Slot 6 (Struggle or Bluff)
            }

            var selected = opponent.SpellSlots[chosenSlot];
            if (selected == null) yield break;

            hudController?.AppendLog($"[CPU] AI choosing move: {selected.displayName} (Slot {chosenSlot + 1})");
            _voice.SimulateSpokenPhrase(selected.displayName);

            yield return new WaitForSeconds(0.6f);

            if (_coordinator.StateMachine.CurrentPhase == BattlePhase.GestureWindow && _coordinator.SelectedSpell != null)
            {
                string gest = _coordinator.SelectedSpell.gestureId;
                hudController?.AppendLog($"[CPU] AI performing gesture: {gest}");
                _gesture.SimulateGesturePerformed(gest, 0.95f);
            }

            yield return new WaitForSeconds(0.6f);

            if (_coordinator.StateMachine.CurrentPhase == BattlePhase.AimPhase)
            {
                hudController?.AppendLog("[CPU] AI acquiring target lock...");
                _ar.SimulateTargetAcquisition(new Vector3(0, 0, 4.5f), 4.5f);
            }

            yield return new WaitForSeconds(0.6f);

            if (_coordinator.StateMachine.CurrentPhase == BattlePhase.AimPhase)
            {
                hudController?.AppendLog("[CPU] AI launching attack!");
                _coordinator.CommitAttackLaunch();
            }
        }

        // Backward compatibility
        public void TestVoiceEmberLance() => TestVoiceSelection();
        public void TestGestureThrust() => TestGestureSuccess();

        private void OnCoordinatorPhaseChanged(object sender, BattlePhaseChangedEventArgs e)
        {
            if (e.CurrentPhase == BattlePhase.AimPhase && !_isRunningAutomatedSlice)
            {
                bool isMyTurn = _coordinator != null && _coordinator.TurnManager != null && _coordinator.TurnManager.IsHostTurn;
                if (isMyTurn)
                {
                    StartCoroutine(AutoLockAndLaunchAttackCoroutine());
                }
            }
        }

        private IEnumerator AutoLockAndLaunchAttackCoroutine()
        {
            if (_isRunningAutomatedSlice) yield break;

            hudController?.AppendLog("<color=#55FF55>✋ HANDS UP CONFIRMED! LOCKING TARGET...</color>");

            // 1. Ensure target lock on opponent
            if (_ar != null)
            {
                _ar.SimulateTargetAcquisition(new Vector3(0, 0, 3.5f), 3.5f);
                _ar.RaycastAimAtHitbox(Vector2.zero);
            }
            if (remoteHitbox != null)
            {
                remoteHitbox.SetTargetLocked(true);
            }

            if (hudController != null && hudController.instructionBannerText != null)
            {
                hudController.instructionBannerText.text = "🎯 TARGET LOCKED! LAUNCHING ATTACK! ⚡";
                hudController.instructionBannerText.color = new Color(0.2f, 1f, 0.4f);
            }

            // 2. Brief 0.15s visual feedback so player sees the green target lock snap onto opponent
            yield return new WaitForSeconds(0.15f);

            // 3. Commit attack launch
            if (_coordinator != null && _coordinator.StateMachine.CurrentPhase == BattlePhase.AimPhase)
            {
                hudController?.AppendLog("<color=#FFD700>⚡ ATTACK LAUNCHED! ⚡</color>");
                _coordinator.CommitAttackLaunch();
            }
        }
    }
}
