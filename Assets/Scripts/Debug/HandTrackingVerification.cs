using System;
using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.AR;
using ArGuildEncounter.Battle;
using ArGuildEncounter.Combat;
using ArGuildEncounter.CPU;
using ArGuildEncounter.Environment;
using ArGuildEncounter.Gesture;
using ArGuildEncounter.Networking;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Voice;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Permanent repository-based automated test suite for Strict Hand-Tracking Gesture Integration.
    /// Covering all Section 13 required test cases and authoritative COMBAT-BALANCE-V1 semantics:
    /// 1. Test_HandsUp_ClassifierGeometry
    /// 2. Test_CurledFist_NotClassifiedAsHandsUp
    /// 3. Test_HandsUp_EmitsSuccessAndGestureThrust
    /// 4. Test_SingleFireGuarantee
    /// 5. Test_NoHandsUp_RemainsWaiting
    /// 6. Test_GestureTimeout_UsesExistingV1TimeoutPath
    /// 7. Test_FailedGesture_UsesExistingPenaltyPath
    /// 8. Test_VoiceAndManualSpellParity
    /// 9. Test_CpuOpponentUnaffected
    /// 10. Test_SpellSelected_HandsUp_AttackFlow
    /// 11. Test_SpellSelected_Timeout_V1AuthoritativePath
    /// 12. Test_TimeoutDoesNotLaunchAttack
    /// 13. Test_TimeoutDoesNotDeductManaTwice
    /// 14. Test_TimeoutDoesNotAdvanceTurnTwice
    /// </summary>
    public static class HandTrackingVerification
    {
        public static bool RunVerification(Action<string> logger = null)
        {
            Action<string> log = logger ?? Debug.Log;
            log("[HAND TRACKING VERIFICATION] Beginning authoritative test suite...");

            bool t1 = Test_HandsUp_ClassifierGeometry(log);
            bool t2 = Test_CurledFist_NotClassifiedAsHandsUp(log);
            bool t3 = Test_HandsUp_EmitsSuccessAndGestureThrust(log);
            bool t4 = Test_SingleFireGuarantee(log);
            bool t5 = Test_NoHandsUp_RemainsWaiting(log);
            bool t6 = Test_GestureTimeout_UsesExistingV1TimeoutPath(log);
            bool t7 = Test_FailedGesture_UsesExistingPenaltyPath(log);
            bool t8 = Test_VoiceAndManualSpellParity(log);
            bool t9 = Test_CpuOpponentUnaffected(log);
            bool t10 = Test_SpellSelected_HandsUp_AttackFlow(log);
            bool t11 = Test_SpellSelected_Timeout_V1AuthoritativePath(log);
            bool t12 = Test_TimeoutDoesNotLaunchAttack(log);
            bool t13 = Test_TimeoutDoesNotDeductManaTwice(log);
            bool t14 = Test_TimeoutDoesNotAdvanceTurnTwice(log);

            bool allPassed = t1 && t2 && t3 && t4 && t5 && t6 && t7 && t8 && t9 && t10 && t11 && t12 && t13 && t14;
            log($"[HAND TRACKING VERIFICATION] Suite Complete. Status: {(allPassed ? "100% PASS (14/14 TESTS)" : "FAILURES DETECTED")}");
            return allPassed;
        }

        // =========================================================================
        // 1. CLASSIFIER GEOMETRIC TESTS
        // =========================================================================

        public static bool Test_HandsUp_ClassifierGeometry(Action<string> log)
        {
            var classifier = new HandsUpClassifier(confidenceThreshold: 0.70f);
            var handLandmarks = HandsUpClassifier.CreateSyntheticHandsUpLandmarks(wristY: 0.55f, fingerLength: 0.28f);

            var result = classifier.Classify(handLandmarks, isYInverted: false);

            bool pass = result.isHandsUp &&
                        result.confidence >= 0.70f &&
                        string.Equals(result.canonicalGestureId, HandsUpClassifier.CanonicalGestureId, StringComparison.OrdinalIgnoreCase) &&
                        result.isOpenPalm &&
                        result.isHandElevated;

            log($"  Test 1: Test_HandsUp_ClassifierGeometry -> {(pass ? "PASS" : "FAIL")} (Conf: {result.confidence:P0}, Gesture: {result.canonicalGestureId}, {result.debugDetails})");
            return pass;
        }

        public static bool Test_CurledFist_NotClassifiedAsHandsUp(Action<string> log)
        {
            var classifier = new HandsUpClassifier(confidenceThreshold: 0.70f);
            var curledFist = HandsUpClassifier.CreateSyntheticCurledFistLandmarks(wristY: 0.40f);

            var result = classifier.Classify(curledFist, isYInverted: false);

            bool pass = !result.isHandsUp && !result.isOpenPalm;
            log($"  Test 2: Test_CurledFist_NotClassifiedAsHandsUp -> {(pass ? "PASS" : "FAIL")} (HandsUp: {result.isHandsUp}, Open: {result.isOpenPalm})");
            return pass;
        }

        // =========================================================================
        // 2. SECTION 13 REQUIRED TESTS
        // =========================================================================

        public static bool Test_HandsUp_EmitsSuccessAndGestureThrust(Action<string> log)
        {
            float simTime = 100f;
            var service = HandTrackingGestureService.CreateInstance(() => simTime, 0.70f);

            bool callbackFired = false;
            string recognizedGesture = "";
            float recognizedConfidence = 0f;

            service.OnGestureRecognized += (r) =>
            {
                callbackFired = true;
                recognizedGesture = r.gestureId;
                recognizedConfidence = r.confidence;
            };

            service.StartGestureRecognition(HandsUpClassifier.CanonicalGestureId, 3.0f);

            var raisedHand = HandsUpClassifier.CreateSyntheticHandsUpLandmarks(wristY: 0.60f);
            service.ProcessHandLandmarks(new[] { raisedHand });

            bool pass = callbackFired &&
                        string.Equals(recognizedGesture, HandsUpClassifier.CanonicalGestureId, StringComparison.OrdinalIgnoreCase) &&
                        recognizedConfidence >= 0.70f &&
                        service.ResultState == GestureResultState.Success &&
                        service.IsHandsUpDetected;

            log($"  Test 3: Test_HandsUp_EmitsSuccessAndGestureThrust -> {(pass ? "PASS" : "FAIL")} (Fired={callbackFired}, Gesture='{recognizedGesture}', Conf={recognizedConfidence:P0}, State={service.ResultState})");
            return pass;
        }

        public static bool Test_SingleFireGuarantee(Action<string> log)
        {
            float simTime = 100f;
            var service = HandTrackingGestureService.CreateInstance(() => simTime, 0.70f);

            int emitCount = 0;
            string emittedGesture = "";
            service.OnGestureRecognized += (r) =>
            {
                emitCount++;
                emittedGesture = r.gestureId;
            };

            // Start gesture recognition window
            service.StartGestureRecognition(HandsUpClassifier.CanonicalGestureId, 3.0f);

            var raisedHand = HandsUpClassifier.CreateSyntheticHandsUpLandmarks(wristY: 0.60f);
            var handArray = new[] { raisedHand };

            // Feed 100 consecutive frames of hands-up (Section 6 Requirement)
            for (int f = 0; f < 100; f++)
            {
                simTime += 0.033f;
                service.ProcessHandLandmarks(handArray);
            }

            bool pass = (emitCount == 1) &&
                        string.Equals(emittedGesture, HandsUpClassifier.CanonicalGestureId, StringComparison.OrdinalIgnoreCase) &&
                        !service.IsTrackingGesture &&
                        (service.ResultState == GestureResultState.Success);

            log($"  Test 4: Test_SingleFireGuarantee (100 frames -> exactly 1 GESTURE_THRUST) -> {(pass ? "PASS" : "FAIL")} (Emitted: {emitCount}, Gesture: '{emittedGesture}', State: {service.ResultState})");
            return pass;
        }

        public static bool Test_NoHandsUp_RemainsWaiting(Action<string> log)
        {
            float simTime = 100f;
            var service = HandTrackingGestureService.CreateInstance(() => simTime);

            int emitCount = 0;
            service.OnGestureRecognized += (_) => emitCount++;

            service.StartGestureRecognition(HandsUpClassifier.CanonicalGestureId, 3.0f);

            var curledFist = HandsUpClassifier.CreateSyntheticCurledFistLandmarks(wristY: 0.30f);
            var handArray = new[] { curledFist };

            for (int f = 0; f < 20; f++)
            {
                simTime += 0.033f;
                service.ProcessHandLandmarks(handArray);
            }

            bool pass = (emitCount == 0) &&
                        service.IsTrackingGesture &&
                        (service.ResultState == GestureResultState.Waiting);

            log($"  Test 5: Test_NoHandsUp_RemainsWaiting -> {(pass ? "PASS" : "FAIL")} (Emitted: {emitCount}, State: {service.ResultState}, Tracking: {service.IsTrackingGesture})");
            return pass;
        }

        public static bool Test_GestureTimeout_UsesExistingV1TimeoutPath(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var net = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var htService = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);

            var coordinator = new BattleCoordinator(config, net, ar, voice, htService, () => 100f);

            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Ruins", null);

            coordinator.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            coordinator.SelectSpellManualFallback(0);
            float castWindow = coordinator.CastTimeRemaining;

            // Authoritative timer expires
            coordinator.TickCastTimer(castWindow + 0.1f);

            bool isCastFailed = !coordinator.LastAttackResult.isCastSuccessful;
            bool summaryMentionsLost = coordinator.LastAttackResult.summaryText != null && coordinator.LastAttackResult.summaryText.Contains("Cast FAILED");

            bool pass = isCastFailed && summaryMentionsLost;
            log($"  Test 6: Test_GestureTimeout_UsesExistingV1TimeoutPath -> {(pass ? "PASS" : "FAIL")} (CastFailed={isCastFailed}, Summary='{coordinator.LastAttackResult.summaryText}')");
            return pass;
        }

        public static bool Test_FailedGesture_UsesExistingPenaltyPath(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var net = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var htService = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);

            var coordinator = new BattleCoordinator(config, net, ar, voice, htService, () => 100f);
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Ruins", null);

            coordinator.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            int p2InitialHp = coordinator.OpponentPlayer.CurrentHP;
            int p1InitialMana = coordinator.LocalPlayer.CurrentMana;

            coordinator.SelectSpellManualFallback(0);
            htService.SimulateGestureFailure();

            bool isCastFailed = !coordinator.LastAttackResult.isCastSuccessful;
            bool hpUnchanged = coordinator.OpponentPlayer.CurrentHP == p2InitialHp;
            int expectedMana = p1InitialMana - 12 + config.defaultManaRegenPerTurn;
            bool manaDeductedOnce = coordinator.LocalPlayer.CurrentMana == expectedMana;
            bool turnAdvanced = coordinator.TurnManager.ActivePlayerId == "Player_2";

            bool pass = isCastFailed && hpUnchanged && manaDeductedOnce && turnAdvanced;
            log($"  Test 7: Test_FailedGesture_UsesExistingPenaltyPath -> {(pass ? "PASS" : "FAIL")} (CastFailed={isCastFailed}, P2 HP={coordinator.OpponentPlayer.CurrentHP}/{p2InitialHp}, Mana={coordinator.LocalPlayer.CurrentMana}/{expectedMana}, NewActive={coordinator.TurnManager.ActivePlayerId})");
            return pass;
        }

        public static bool Test_VoiceAndManualSpellParity(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var net = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Ruins", null);

            // 1. Voice Selection
            var voice1 = new MockVoiceInputService();
            var ht1 = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);
            var coord1 = new BattleCoordinator(config, net, ar, voice1, ht1, () => 100f);
            coord1.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena);
            coord1.StartBattle(useDebugQuickStart: true);

            voice1.StartListening(coord1.LocalPlayer.SpellSlots);
            voice1.SimulateSpokenPhrase("Fireball", coord1.LocalPlayer.SpellSlots);
            string voiceSpellId = coord1.SelectedSpell?.spellId ?? "";

            // 2. Manual Selection
            var voice2 = new MockVoiceInputService();
            var ht2 = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);
            var coord2 = new BattleCoordinator(config, net, ar, voice2, ht2, () => 100f);
            coord2.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena);
            coord2.StartBattle(useDebugQuickStart: true);

            coord2.SelectSpellManualFallback(0);
            string manualSpellId = coord2.SelectedSpell?.spellId ?? "";

            bool pass = string.Equals(voiceSpellId, manualSpellId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(voiceSpellId);

            log($"  Test 8: Test_VoiceAndManualSpellParity -> {(pass ? "PASS" : "FAIL")} (Voice='{voiceSpellId}', Manual='{manualSpellId}')");
            return pass;
        }

        public static bool Test_CpuOpponentUnaffected(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var net = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var htService = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);

            var coordinator = new BattleCoordinator(config, net, ar, voice, htService, () => 100f);
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Ruins", null);

            coordinator.SetupMatch("Player_1", magma, spells1, "CPU_Player", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            // Execute Turn 1 (Player_1) to naturally advance turn to CPU_Player
            coordinator.SelectSpellManualFallback(0);
            htService.SimulateHandsUp(0.95f);
            ar.SimulateTargetAcquisition(new Vector3(0, 0, 3.5f), 3.5f);
            coordinator.CommitAttackLaunch();

            // Assert Turn 2 is CPU turn and entered SpellSelection
            bool isCpuTurn = coordinator.TurnManager.ActivePlayerId == "CPU_Player";
            bool inSpellSelection = coordinator.StateMachine.CurrentPhase == BattlePhase.SpellSelection;

            // CPU selects move
            coordinator.SelectSpellManualFallback(0);
            bool inGestureWindow = coordinator.StateMachine.CurrentPhase == BattlePhase.GestureWindow;

            // CPU does NOT use camera or landmark processing; uses SimulateGesturePerformed directly
            coordinator.GestureInput.SimulateGesturePerformed(HandsUpClassifier.CanonicalGestureId, 0.95f);
            bool inAimPhase = coordinator.StateMachine.CurrentPhase == BattlePhase.AimPhase;

            bool pass = isCpuTurn && inSpellSelection && inGestureWindow && inAimPhase;
            log($"  Test 9: Test_CpuOpponentUnaffected -> {(pass ? "PASS" : "FAIL")} (CPU Active: {isCpuTurn}, SpellSelect: {inSpellSelection}, GestureWin: {inGestureWindow}, Aim: {inAimPhase})");
            return pass;
        }

        // =========================================================================
        // 3. EXPLICIT INTEGRATION & TIMEOUT TESTS (Section 13)
        // =========================================================================

        public static bool Test_SpellSelected_HandsUp_AttackFlow(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var net = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var htService = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);

            var coordinator = new BattleCoordinator(config, net, ar, voice, htService, () => 100f);

            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Ruins", null);

            coordinator.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            int p2InitialHp = coordinator.OpponentPlayer.CurrentHP;
            int p1InitialMana = coordinator.LocalPlayer.CurrentMana;

            coordinator.SelectSpellManualFallback(0);
            if (coordinator.StateMachine.CurrentPhase != BattlePhase.GestureWindow)
            {
                log($"  Test 10: Test_SpellSelected_HandsUp_AttackFlow -> FAIL (Phase={coordinator.StateMachine.CurrentPhase})");
                return false;
            }

            // Feed hands-up landmark frame into HandTrackingGestureService
            var raisedHand = HandsUpClassifier.CreateSyntheticHandsUpLandmarks(wristY: 0.60f);
            htService.ProcessHandLandmarks(new[] { raisedHand });

            bool inAim = coordinator.StateMachine.CurrentPhase == BattlePhase.AimPhase;

            // Commit attack launch
            ar.SimulateTargetAcquisition(new Vector3(0, 0, 3.5f), 3.5f);
            bool launchOk = coordinator.CommitAttackLaunch();

            bool damageResolved = coordinator.OpponentPlayer.CurrentHP < p2InitialHp;
            bool manaDeducted = coordinator.LocalPlayer.CurrentMana == (p1InitialMana - 12 + config.defaultManaRegenPerTurn);

            bool pass = inAim && launchOk && damageResolved && manaDeducted;
            log($"  Test 10: Test_SpellSelected_HandsUp_AttackFlow -> {(pass ? "PASS" : "FAIL")} (AimPhase={inAim}, LaunchOk={launchOk}, DamageResolved={damageResolved}, P2 HP={coordinator.OpponentPlayer.CurrentHP})");
            return pass;
        }

        public static bool Test_SpellSelected_Timeout_V1AuthoritativePath(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var net = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var htService = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);

            var coordinator = new BattleCoordinator(config, net, ar, voice, htService, () => 100f);

            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Ruins", null);

            coordinator.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            int p2InitialHp = coordinator.OpponentPlayer.CurrentHP;
            int p1InitialMana = coordinator.LocalPlayer.CurrentMana;

            coordinator.SelectSpellManualFallback(0);
            coordinator.TickCastTimer(coordinator.CastTimeRemaining + 0.1f);

            // Assertions for V1 timeout path:
            bool hpUnchanged = coordinator.OpponentPlayer.CurrentHP == p2InitialHp;
            int expectedMana = p1InitialMana - 12 + config.defaultManaRegenPerTurn;
            bool manaDeductedOnce = coordinator.LocalPlayer.CurrentMana == expectedMana;
            bool turnAdvancedOnce = coordinator.TurnManager.ActivePlayerId == "Player_2";
            bool noAttack = !coordinator.LastAttackResult.isHit;

            bool pass = hpUnchanged && manaDeductedOnce && turnAdvancedOnce && noAttack;
            log($"  Test 11: Test_SpellSelected_Timeout_V1AuthoritativePath -> {(pass ? "PASS" : "FAIL")} (P2 HP={coordinator.OpponentPlayer.CurrentHP}/{p2InitialHp}, Mana={coordinator.LocalPlayer.CurrentMana}/{expectedMana}, TurnActive={coordinator.TurnManager.ActivePlayerId})");
            return pass;
        }

        public static bool Test_TimeoutDoesNotLaunchAttack(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var net = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var htService = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);

            var coordinator = new BattleCoordinator(config, net, ar, voice, htService, () => 100f);
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Ruins", null);

            coordinator.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            int p2InitialHp = coordinator.OpponentPlayer.CurrentHP;

            coordinator.SelectSpellManualFallback(0);
            coordinator.TickCastTimer(coordinator.CastTimeRemaining + 0.1f);

            // Assert Opponent HP is completely unchanged
            bool hpUnchanged = coordinator.OpponentPlayer.CurrentHP == p2InitialHp;
            bool attackNotHit = !coordinator.LastAttackResult.isHit;

            bool pass = hpUnchanged && attackNotHit;
            log($"  Test 12: Test_TimeoutDoesNotLaunchAttack -> {(pass ? "PASS" : "FAIL")} (P2 HP Before={p2InitialHp}, After={coordinator.OpponentPlayer.CurrentHP}, AttackHit={coordinator.LastAttackResult.isHit})");
            return pass;
        }

        public static bool Test_TimeoutDoesNotDeductManaTwice(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var net = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var htService = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);

            var coordinator = new BattleCoordinator(config, net, ar, voice, htService, () => 100f);
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Ruins", null);

            coordinator.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            int p1InitialMana = coordinator.LocalPlayer.CurrentMana; // 100
            int spellCost = coordinator.LocalPlayer.SpellSlots[0].manaCost; // 12
            int regen = config.defaultManaRegenPerTurn; // 10
            int expectedMana = p1InitialMana - spellCost + regen; // 98

            coordinator.SelectSpellManualFallback(0);
            coordinator.TickCastTimer(coordinator.CastTimeRemaining + 0.1f);

            int actualMana = coordinator.LocalPlayer.CurrentMana;
            bool manaDeductedExactlyOnce = actualMana == expectedMana;

            log($"  Test 13: Test_TimeoutDoesNotDeductManaTwice -> {(manaDeductedExactlyOnce ? "PASS" : "FAIL")} (Initial={p1InitialMana}, Cost={spellCost}, Expected={expectedMana}, Actual={actualMana})");
            return manaDeductedExactlyOnce;
        }

        public static bool Test_TimeoutDoesNotAdvanceTurnTwice(Action<string> log)
        {
            var config = TestCombatDataFactory.CreateTestCombatConfig();
            config.firstPlayerRule = FirstPlayerRule.HostFirst;
            var net = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var htService = HandTrackingGestureService.CreateInstance(() => 100f, 0.70f);

            var coordinator = new BattleCoordinator(config, net, ar, voice, htService, () => 100f);
            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Ruins", null);

            coordinator.SetupMatch("Player_1", magma, spells1, "Player_2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            int initialTurnNumber = coordinator.TurnManager.CurrentTurnNumber; // 1
            string initialActivePlayer = coordinator.TurnManager.ActivePlayerId; // Player_1

            coordinator.SelectSpellManualFallback(0);
            coordinator.TickCastTimer(coordinator.CastTimeRemaining + 0.1f);

            int finalTurnNumber = coordinator.TurnManager.CurrentTurnNumber; // Should be 2
            string finalActivePlayer = coordinator.TurnManager.ActivePlayerId; // Should be Player_2

            bool advancedExactlyOnce = (finalTurnNumber == initialTurnNumber + 1) &&
                                       (finalActivePlayer == "Player_2");

            log($"  Test 14: Test_TimeoutDoesNotAdvanceTurnTwice -> {(advancedExactlyOnce ? "PASS" : "FAIL")} (Turn={initialTurnNumber}->{finalTurnNumber}, Active={initialActivePlayer}->{finalActivePlayer})");
            return advancedExactlyOnce;
        }
    }
}
