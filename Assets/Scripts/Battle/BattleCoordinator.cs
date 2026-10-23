using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Environment;
using ArGuildEncounter.Networking;
using ArGuildEncounter.AR;
using ArGuildEncounter.Voice;
using ArGuildEncounter.Gesture;

namespace ArGuildEncounter.Battle
{
    /// <summary>
    /// Master coordinator for the AR Guild Encounter battle session.
    /// Integrates the authoritative state machine, combat resolution, hardware abstractions,
    /// network synchronization, and player state.
    /// </summary>
    public class BattleCoordinator
    {
        // Core Systems
        public CombatConfig Config { get; }
        public IBattleStateMachine StateMachine { get; }
        public TurnManager TurnManager { get; private set; }
        public AttackResolutionPipeline ResolutionPipeline { get; }

        // Players
        public PlayerCombatState LocalPlayer { get; private set; }
        public PlayerCombatState OpponentPlayer { get; private set; }
        public PlayerEntity LocalEntity { get; private set; }
        public PlayerEntity OpponentEntity { get; private set; }
        public PlayerHitbox LocalHitbox { get; private set; }
        public PlayerHitbox OpponentHitbox { get; private set; }
        public EnvironmentData CurrentEnvironment { get; set; }
        public TerrainData CurrentTerrain { get; set; }
        public ArenaData CurrentArena { get; set; }

        // Hardware / Network Abstractions
        public INetworkSessionProvider NetworkSession { get; }
        public IARTrackingService ARTracking { get; }
        public IVoiceInputService VoiceInput { get; }
        public IGestureService GestureInput { get; }
        public DisconnectManager DisconnectManager { get; }

        // Active Turn Transient State
        public SpellData SelectedSpell { get; private set; }
        public float CastTimeRemaining { get; private set; }
        public bool IsGestureCompleted { get; private set; }
        public AttackResolutionResult LastAttackResult { get; private set; }

        // Path Selection State
        public PathDefinition LocalPlayerPath { get; private set; }
        public PathDefinition OpponentPlayerPath { get; private set; }
        public CombatPathConfig PathConfig { get; set; }

        public ISpellDataProvider SpellDataProvider { get; set; }
        private bool _isResolvingAttack = false;
        public bool IsResolvingAttack => _isResolvingAttack;
        public bool AutoAdvanceAfterAttack { get; set; } = true;
        public bool IsCpuOpponentEnabled { get; set; } = false;

        // Events
        public event Action OnMatchSetupCompleted;
        public event Action OnMatchReset;
        public event Action<string> OnBattleLog;
        public event Action<string> OnSpellSelectionRejected;
        public event Action<AttackResolutionResult> OnAttackResolved;
        public event Action<AttackResolutionResult> OnAttackPresentationRequested;
        public event Action OnCpuTurnRequested;
        public event Action<PathDefinition, PathDefinition> OnPathsAssigned;
        public event Action<float> OnCastTimerUpdated;

        public BattleCoordinator(
            CombatConfig config,
            INetworkSessionProvider network,
            IARTrackingService ar,
            IVoiceInputService voice,
            IGestureService gesture,
            Func<float> timeProvider = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            NetworkSession = network ?? throw new ArgumentNullException(nameof(network));
            ARTracking = ar ?? throw new ArgumentNullException(nameof(ar));
            VoiceInput = voice ?? throw new ArgumentNullException(nameof(voice));
            GestureInput = gesture ?? throw new ArgumentNullException(nameof(gesture));

            StateMachine = new BattleStateMachine(timeProvider);
            ResolutionPipeline = new AttackResolutionPipeline(Config);
            DisconnectManager = new DisconnectManager(StateMachine, NetworkSession);

            HookServiceEvents();
        }

        private void HookServiceEvents()
        {
            VoiceInput.OnSpeechRecognized += HandleSpeechRecognized;
            VoiceInput.OnSpeechUnrecognized += HandleSpeechUnrecognized;

            GestureInput.OnGestureRecognized += HandleGestureRecognized;
            GestureInput.OnGestureFailed += HandleGestureFailed;
            GestureInput.OnGestureWindowTimedOut += HandleGestureTimeout;

            ARTracking.OnTargetLost += HandleARTrackingLost;
            ARTracking.OnTargetReacquired += HandleARTrackingReacquired;
        }

        public void SetupMatch(
            string localPlayerId, 
            GuildData localGuild, 
            List<SpellData> localSpells,
            string opponentPlayerId, 
            GuildData opponentGuild, 
            List<SpellData> opponentSpells,
            ArenaData arena,
            TerrainData terrain = null,
            PathDefinition localPath = null,
            PathDefinition opponentPath = null)
        {
            CurrentArena = arena;
            CurrentEnvironment = (object)arena != null ? arena.defaultEnvironment : null;
            CurrentTerrain = terrain != null ? terrain : AuthoritativeCombatRoster.GetTerrain("VOLCANIC_RUINS");

            // Determine fallback ability (Struggle for normal, Bluff for Gambit)
            ISpellDataProvider provider = SpellDataProvider;
            SpellData localFallback = ((object)localGuild != null && localGuild.isGambitGuild)
                ? (provider != null ? provider.GetSpell("BLUFF") : AuthoritativeCombatRoster.GetSpell("BLUFF"))
                : (provider != null ? provider.GetSpell("STRUGGLE") : AuthoritativeCombatRoster.GetSpell("STRUGGLE"));

            SpellData opponentFallback = ((object)opponentGuild != null && opponentGuild.isGambitGuild)
                ? (provider != null ? provider.GetSpell("BLUFF") : AuthoritativeCombatRoster.GetSpell("BLUFF"))
                : (provider != null ? provider.GetSpell("STRUGGLE") : AuthoritativeCombatRoster.GetSpell("STRUGGLE"));

            LocalPlayer = new PlayerCombatState(localPlayerId, Config, localGuild);
            LocalPlayer.SetLoadout(localSpells, localFallback);

            OpponentPlayer = new PlayerCombatState(opponentPlayerId, Config, opponentGuild);
            OpponentPlayer.SetLoadout(opponentSpells, opponentFallback);

            LocalEntity = new PlayerEntity(localPlayerId, isLocal: true, isHost: true, localGuild) { CombatState = LocalPlayer };
            OpponentEntity = new PlayerEntity(opponentPlayerId, isLocal: false, isHost: false, opponentGuild) { CombatState = OpponentPlayer };

            if (localPath != null || opponentPath != null)
            {
                AssignPlayerPaths(localPath, opponentPath);
            }

            TurnManager = new TurnManager(Config, localPlayerId, opponentPlayerId, LocalPlayer, OpponentPlayer);
            TurnManager.TurnStarted += HandleTurnStarted;

            Log($"Match setup complete. Arena: {((object)arena != null ? arena.arenaName : "None")}. Terrain: {((object)CurrentTerrain != null ? CurrentTerrain.terrainName : "None")}.");
            OnMatchSetupCompleted?.Invoke();
        }

        public void AssignPlayerPaths(PathDefinition localPath, PathDefinition opponentPath)
        {
            LocalPlayerPath = localPath;
            OpponentPlayerPath = opponentPath;

            if (LocalPlayer != null && (object)localPath != null)
            {
                LocalPlayer.ApplyPathModifiers(localPath);
                Log($"[PATH] Local player applied path '{localPath.displayName}' (HP Mod: {localPath.payload.maxHpModifier}, Attack Bonus: +{localPath.payload.attackMultiplierBonus * 100f:0}%, Dmg Reduction: {localPath.payload.damageReductionModifier * 100f:0}%, Mana Regen: +{localPath.payload.bonusManaRegenPerTurn}).");
            }

            if (OpponentPlayer != null && (object)opponentPath != null)
            {
                OpponentPlayer.ApplyPathModifiers(opponentPath);
                Log($"[PATH] Opponent player applied path '{opponentPath.displayName}'.");
            }

            OnPathsAssigned?.Invoke(localPath, opponentPath);
        }

        public void BindDualHitboxes(PlayerHitbox localHitbox, PlayerHitbox opponentHitbox)
        {
            LocalHitbox = localHitbox;
            OpponentHitbox = opponentHitbox;

            if (LocalHitbox != null && LocalPlayer != null)
            {
                LocalHitbox.OwnerPlayerId = LocalPlayer.PlayerId;
                LocalHitbox.IsLocalPlayer = true;
                LocalEntity?.BindHitbox(LocalHitbox);
            }

            if (OpponentHitbox != null && OpponentPlayer != null)
            {
                OpponentHitbox.OwnerPlayerId = OpponentPlayer.PlayerId;
                OpponentHitbox.IsLocalPlayer = false;
                OpponentEntity?.BindHitbox(OpponentHitbox);
            }
        }

        public bool ValidateTargetAcquisition(string targetPlayerId, PlayerHitbox hitbox)
        {
            // Critical POV Rule: Cannot target local player
            if (LocalPlayer != null && string.Equals(targetPlayerId, LocalPlayer.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                Log("[TARGET REJECTED] Cannot target self!");
                return false;
            }

            if (hitbox == null || hitbox.IsLocalPlayer || !hitbox.CanBeTargetedBy(LocalPlayer?.PlayerId))
            {
                return false;
            }

            return true;
        }

        public void StartBattle(bool useDebugQuickStart = false)
        {
            if (useDebugQuickStart)
            {
                if (StateMachine.CurrentPhase == BattlePhase.WaitingForPlayer)
                {
                    StateMachine.TryTransition(BattlePhase.PlayerJoined, "Debug quick-start player joined");
                }
                StateMachine.AllowDebugQuickStart = true;
                if (StateMachine.TryDebugQuickStart("Debug quick-start battle"))
                {
                    TurnManager.InitializeFirstTurn();
                    return;
                }
            }

            if (StateMachine.TryTransition(BattlePhase.BattleReady, "Loadouts confirmed"))
            {
                TurnManager.InitializeFirstTurn();
            }
        }

        public void ResetMatch()
        {
            SelectedSpell = null;
            CastTimeRemaining = 0f;
            IsGestureCompleted = false;
            LastAttackResult = default;
            LocalPlayerPath = null;
            OpponentPlayerPath = null;
            LocalPlayer?.ResetCombatState();
            OpponentPlayer?.ResetCombatState();
            TurnManager?.Reset();
            ARTracking?.SimulateTargetLost();
            VoiceInput?.StopListening();
            GestureInput?.StopGestureRecognition();
            ResolutionPipeline.ForceMissDebug = false;
            ResolutionPipeline.ForceCritDebug = false;
            _isResolvingAttack = false;
            StateMachine.ForcePhase(BattlePhase.WaitingForPlayer, "Match reset");
            OnMatchReset?.Invoke();
            Log("[MATCH RESET] Battle coordinator returned to WaitingForPlayer.");
        }

        private void HandleTurnStarted(object sender, TurnStartedEventArgs e)
        {
            SelectedSpell = null;
            IsGestureCompleted = false;
            _isResolvingAttack = false;

            PlayerCombatState activeState = TurnManager.GetActivePlayerState();
            Log($"[TURN {e.TurnNumber}] {(e.IsHostTurn ? "YOUR TURN" : "OPPONENT'S TURN")} (Active: {activeState?.PlayerId})");

            if (StateMachine.TryTransition(BattlePhase.PlayerTurn, $"Turn {e.TurnNumber} begin"))
            {
                if (activeState != null && activeState.ConsumeMoveSkipIfActive())
                {
                    Log($"[CONTROL] {activeState.PlayerId}'s action was SKIPPED due to Move Skip! 1-action Control Immunity granted.");
                    activeState.RegenerateEndOfTurnMana();
                    if (StateMachine.TryTransition(BattlePhase.NextTurn, "Turn skipped due to Move Skip"))
                    {
                        TurnManager.AdvanceTurn();
                    }
                    return;
                }

                StateMachine.TryTransition(BattlePhase.SpellSelection, $"Prompting {activeState?.PlayerId} for spell summoning");
                if (activeState != null)
                {
                    VoiceInput.StartListening(activeState.SpellSlots);
                }

                if (IsCpuOpponentEnabled && activeState != null && OpponentPlayer != null && activeState.PlayerId == OpponentPlayer.PlayerId)
                {
                    OnCpuTurnRequested?.Invoke();
                }
            }
        }

        private void HandleSpeechRecognized(VoiceRecognitionResult result)
        {
            if (StateMachine.CurrentPhase != BattlePhase.SpellSelection &&
                StateMachine.CurrentPhase != BattlePhase.VoiceProcessing)
            {
                return;
            }

            PlayerCombatState activePlayer = TurnManager?.GetActivePlayerState();
            if (activePlayer == null || result.matchedSpell is null)
            {
                OnSpellSelectionRejected?.Invoke("INVALID STATE OR NULL SPELL");
                return;
            }

            int slotIndex = -1;
            for (int i = 0; i < activePlayer.SpellSlots.Length; i++)
            {
                if (activePlayer.SpellSlots[i] is not null &&
                    (activePlayer.SpellSlots[i] == result.matchedSpell ||
                     string.Equals(activePlayer.SpellSlots[i].spellId, result.matchedSpell.spellId, StringComparison.OrdinalIgnoreCase)))
                {
                    slotIndex = i;
                    break;
                }
            }

            // Strictly validate that spell belongs to active player's legal loadout
            if (slotIndex < 0)
            {
                Log($"SPELL REJECTED: '{result.matchedSpell.displayName}' is not in {activePlayer.PlayerId}'s legal loadout.");
                OnSpellSelectionRejected?.Invoke($"NOT IN LOADOUT: {result.matchedSpell.displayName}");
                return;
            }

            if (!activePlayer.CanCastSpell(slotIndex, out string failureReason))
            {
                if (slotIndex == LoadoutValidator.FallbackSlotIndex && !activePlayer.IsSlot6Selectable)
                {
                    Log("Slot 6 is locked: You still have affordable moves in Slots 1-5!");
                    OnSpellSelectionRejected?.Invoke("SLOT 6 LOCKED: Moves available in Slots 1-5");
                    return;
                }

                Log($"CANNOT CAST: {result.matchedSpell.spellName} - {failureReason} (Cost: {result.matchedSpell.manaCost}, Mana: {activePlayer.CurrentMana})");
                OnSpellSelectionRejected?.Invoke($"ACTION REJECTED: {failureReason}");
                return;
            }

            SelectedSpell = activePlayer.SpellSlots[slotIndex];
            VoiceInput.StopListening();
            Log($"Voice Recognized: Summoning '{SelectedSpell.displayName}' (Confidence: {result.confidence:P0})");

            if (StateMachine.TryTransition(BattlePhase.SpellSelected, $"Spell summoned: {SelectedSpell.displayName}"))
            {
                BeginGestureWindow(SelectedSpell);
            }
        }

        private void HandleSpeechUnrecognized(string phrase)
        {
            Log($"Voice Unrecognized: '{phrase}'. Please retry or tap spell directly.");
        }

        public bool SelectSpellManualFallback(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6) return false;
            if (StateMachine.CurrentPhase != BattlePhase.SpellSelection &&
                StateMachine.CurrentPhase != BattlePhase.VoiceProcessing)
            {
                return false;
            }

            PlayerCombatState activePlayer = TurnManager.GetActivePlayerState();
            var spell = activePlayer.SpellSlots[slotIndex];
            if (spell is null) return false;

            if (activePlayer.HasForceFallback && slotIndex != LoadoutValidator.FallbackSlotIndex)
            {
                Log("FORCED FALLBACK ACTIVE: You must use Slot 6!");
                OnSpellSelectionRejected?.Invoke("FORCED FALLBACK: Must use Slot 6");
                return false;
            }

            if (!activePlayer.CanCastSpell(spell, slotIndex))
            {
                if (slotIndex == LoadoutValidator.FallbackSlotIndex && !activePlayer.IsSlot6Selectable)
                {
                    Log("Slot 6 is locked: You still have affordable moves in Slots 1-5!");
                    OnSpellSelectionRejected?.Invoke("SLOT 6 LOCKED: Moves available in Slots 1-5");
                    return false;
                }

                Log($"INSUFFICIENT MANA: Cannot cast {spell.spellName} (Cost: {spell.manaCost}, Mana: {activePlayer.Mana})");
                OnSpellSelectionRejected?.Invoke($"INSUFFICIENT MANA: Needs {spell.manaCost}, have {activePlayer.Mana}");
                return false;
            }

            SelectedSpell = spell;
            VoiceInput.TriggerManualFallbackSelection(spell);
            return true;
        }

        private void BeginGestureWindow(SpellData spell)
        {
            if (!StateMachine.TryTransition(BattlePhase.GestureWindow, "Starting authoritative gesture timer"))
            {
                return;
            }

            float window = Config.GetCastWindowForTier(spell.tier);
            CastTimeRemaining = window;
            OnCastTimerUpdated?.Invoke(CastTimeRemaining);

            Log($"Cast Window: {window}s. Perform Gesture: '{spell.gestureId}'");
            GestureInput.StartGestureRecognition(spell.gestureId, window);
        }

        private void HandleGestureRecognized(GestureRecognitionResult result)
        {
            if (StateMachine.CurrentPhase != BattlePhase.GestureWindow) return;

            IsGestureCompleted = true;
            GestureInput.StopGestureRecognition();
            Log($"Gesture Validated! Hand shape '{result.gestureId}' confirmed ({result.confidence:P0}).");

            if (StateMachine.TryTransition(BattlePhase.AimPhase, "Gesture complete. Aim at opponent hitbox."))
            {
                // In aim phase, reticle tracks target at screen center
                ARTracking.RaycastAimAtHitbox(Vector2.zero);
            }
        }

        private void HandleGestureFailed()
        {
            if (StateMachine.CurrentPhase != BattlePhase.GestureWindow) return;

            _isResolvingAttack = true;
            GestureInput.StopGestureRecognition();
            Log("Cast FAILED! Incorrect gesture. Turn ends and mana penalty is applied.");

            ExecuteResolutionPipeline(isGestureValid: false, isTargetAcquired: false);
        }

        private void HandleGestureTimeout()
        {
            if (StateMachine.CurrentPhase != BattlePhase.GestureWindow) return;

            _isResolvingAttack = true;
            GestureInput.StopGestureRecognition();
            Log("Cast TIMEOUT! Authoritative timer reached 0. Attack missed.");

            ExecuteResolutionPipeline(isGestureValid: false, isTargetAcquired: false);
        }

        public void TriggerCastTimeout()
        {
            if (StateMachine.CurrentPhase == BattlePhase.GestureWindow)
            {
                HandleGestureTimeout();
            }
        }

        public void TickCastTimer(float deltaTime)
        {
            if (StateMachine.CurrentPhase != BattlePhase.GestureWindow) return;

            CastTimeRemaining -= deltaTime;
            OnCastTimerUpdated?.Invoke(CastTimeRemaining);

            if (GestureInput is ArGuildEncounter.Gesture.HandTrackingGestureService htService)
            {
                htService.UpdateObservedTimeRemaining(CastTimeRemaining);
            }

            if (CastTimeRemaining <= 0f)
            {
                CastTimeRemaining = 0f;
                HandleGestureTimeout();
            }
        }

        public bool CommitAttackLaunch(string targetPlayerId = null)
        {
            if (_isResolvingAttack) return false;
            if (StateMachine.CurrentPhase != BattlePhase.AimPhase) return false;

            // Critical POV Rule: Cannot target local player
            if (!string.IsNullOrEmpty(targetPlayerId) && LocalPlayer != null &&
                string.Equals(targetPlayerId, LocalPlayer.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                Log("[TARGET REJECTED] Cannot target self!");
                return false;
            }

            _isResolvingAttack = true;
            bool targetAcquired = ARTracking.TargetStatus.isVisible;
            if (StateMachine.TryTransition(BattlePhase.AttackLaunched, "Attack launched at target"))
            {
                ExecuteResolutionPipeline(isGestureValid: true, isTargetAcquired: targetAcquired);
                return true;
            }
            _isResolvingAttack = false;
            return false;
        }

        private void ExecuteResolutionPipeline(bool isGestureValid, bool isTargetAcquired)
        {
            _isResolvingAttack = true;
            if (!StateMachine.TryTransition(BattlePhase.AttackResolution, "Resolving combat math"))
            {
                _isResolvingAttack = false;
                return;
            }

            PlayerCombatState attacker = TurnManager.GetActivePlayerState();
            PlayerCombatState defender = TurnManager.GetWaitingPlayerState();

            // Attacker consumes Force Fallback if active (granting Control Immunity)
            attacker.ConsumeForceFallbackIfActive();

            var bluffPool = SpellDataProvider != null ? SpellDataProvider.GetBluffPool() : AuthoritativeCombatRoster.GetBluffEligiblePool();

            LastAttackResult = ResolutionPipeline.ResolveAttack(
                attacker,
                defender,
                SelectedSpell,
                CurrentEnvironment,
                CurrentTerrain,
                isGestureValid,
                isTargetAcquired,
                null,
                bluffPool);

            Log($"[RESOLUTION] {LastAttackResult.summaryText}");
            OnAttackResolved?.Invoke(LastAttackResult);
            OnAttackPresentationRequested?.Invoke(LastAttackResult);

            if (AutoAdvanceAfterAttack)
            {
                CompleteAttackAndAdvanceTurn();
            }
        }

        public void CompleteAttackAndAdvanceTurn()
        {
            PlayerCombatState attacker = TurnManager?.GetActivePlayerState();
            PlayerCombatState defender = TurnManager?.GetWaitingPlayerState();

            if (attacker == null || defender == null)
            {
                _isResolvingAttack = false;
                return;
            }

            // Advance to HP Check
            if (StateMachine.TryTransition(BattlePhase.HPCheck, "Checking victory conditions"))
            {
                if (!defender.IsAlive && !attacker.IsAlive)
                {
                    Log("[MATCH END] Double KO! The match ends in a DRAW.");
                    StateMachine.TryTransition(BattlePhase.BattleEnd, "Draw - Double KO");
                }
                else if (!defender.IsAlive)
                {
                    Log($"[MATCH END] {defender.PlayerId} HP reached 0. {attacker.PlayerId} WINS!");
                    StateMachine.TryTransition(BattlePhase.BattleEnd, $"{attacker.PlayerId} defeated {defender.PlayerId}");
                }
                else if (!attacker.IsAlive)
                {
                    Log($"[MATCH END] {attacker.PlayerId} took lethal backlash! {defender.PlayerId} WINS!");
                    StateMachine.TryTransition(BattlePhase.BattleEnd, $"{attacker.PlayerId} eliminated");
                }
                else
                {
                    // Both combatants alive -> apply end-of-turn mana regeneration to acting player and advance turn
                    attacker.RegenerateEndOfTurnMana();
                    if (StateMachine.TryTransition(BattlePhase.NextTurn, "Turn cycle complete"))
                    {
                        TurnManager.AdvanceTurn();
                    }
                }
            }

            _isResolvingAttack = false;
        }

        public void ApplyStatusDebug(string statusName, bool targetOpponent)
        {
            var target = targetOpponent ? OpponentPlayer : LocalPlayer;
            if (target == null) return;
            string s = statusName?.ToUpperInvariant() ?? "BURN";
            if (s.Contains("BURN")) target.ApplyFlameLegion();
            else if (s.Contains("CHILL") || s.Contains("FREEZE")) target.ApplyHydrationSurge();
            else if (s.Contains("BLIND")) target.ApplyBlindness();
            else if (s.Contains("SHOCK") || s.Contains("SWAMP")) target.ApplyDampenedSight();
            else if (s.Contains("SUFFOCAT") || s.Contains("GAS")) target.ApplySuffocation();
            else target.ApplyEmberWard();
            Log($"[DEBUG] Injected status '{statusName}' onto {target.PlayerId}.");
        }

        public bool ConfirmGuildSelection(GuildData guild)
        {
            if ((object)guild == null || LocalPlayer == null) return false;
            LocalPlayer.Guild = guild;
            Log($"[GUILD SELECTION] Local player chose {guild.displayName}.");
            return StateMachine.TryTransition(BattlePhase.Loadout, "Guild confirmed");
        }

        public bool ConfirmLoadoutAndStartBattle()
        {
            if (StateMachine.TryTransition(BattlePhase.BattleReady, "Loadout confirmed"))
            {
                TurnManager.InitializeFirstTurn();
                return true;
            }
            return false;
        }

        private void HandleARTrackingLost()
        {
            if (!StateMachine.IsInBattleLoop && StateMachine.CurrentPhase != BattlePhase.ARCalibration) return;
            Log("AR Tracking Lost: Searching for opponent...");
            StateMachine.TryTransition(BattlePhase.ARTrackingLost, "Camera obscured or target lost");
        }

        private void HandleARTrackingReacquired()
        {
            Log("AR Tracking Reacquired: Resuming battle.");
            StateMachine.TryTransition(StateMachine.StoredInterruptedPhase, "Target reacquired");
        }

        private void Log(string message)
        {
            OnBattleLog?.Invoke(message);
        }
    }
}
