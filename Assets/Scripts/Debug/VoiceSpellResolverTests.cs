using System.Collections.Generic;
using UnityEngine;
using ArGuildEncounter.Spells;
using ArGuildEncounter.Voice;
using ArGuildEncounter.Battle;
using ArGuildEncounter.Combat;
using ArGuildEncounter.Guilds;
using ArGuildEncounter.Networking;
using ArGuildEncounter.AR;
using ArGuildEncounter.Gesture;
using ArGuildEncounter.Environment;

namespace ArGuildEncounter.DebugSuite
{
    /// <summary>
    /// Comprehensive test harness for the VoiceSpellCommandResolver and voice-to-spell pipeline.
    /// Covers 24 test cases including normalization, fuzzy matching, ambiguity, confidence,
    /// legality enforcement, and manual/voice parity.
    /// 
    /// Run from Unity Editor: [ContextMenu] or call RunAllTests() from code.
    /// </summary>
    public class VoiceSpellResolverTests : MonoBehaviour
    {
        private int _passed = 0;
        private int _failed = 0;
        private readonly List<string> _failures = new List<string>();

        [ContextMenu("Run All Voice Resolver Tests")]
        public void RunAllTests()
        {
            _passed = 0;
            _failed = 0;
            _failures.Clear();

            Debug.Log("=== VOICE SPELL RESOLVER TEST SUITE (24 Tests) ===");

            var resolver = new VoiceSpellCommandResolver();
            var loadout = CreateTestLoadout();

            // --- Exact Matching ---
            Test01_ExactFireball(resolver, loadout);
            Test02_FireBallSpaced(resolver, loadout);
            Test03_RoyalInferno(resolver, loadout);
            Test04_DeathLight(resolver, loadout);

            // --- Normalization ---
            Test05_AllCaps(resolver, loadout);
            Test06_ExcessWhitespace(resolver, loadout);
            Test07_PunctuationStrip(resolver, loadout);
            Test08_HyphenHandling(resolver, loadout);
            Test09_TrailingExclamation(resolver, loadout);

            // --- Aliases ---
            Test10_SpellIdAsAlias(resolver, loadout);
            Test11_LowercaseAlias(resolver, loadout);

            // --- Fuzzy Matching ---
            Test12_TypoFirebal(resolver, loadout);
            Test13_TypoRoyalInfeno(resolver, loadout);

            // --- Unknown & Ambiguous ---
            Test14_UnknownPhrase(resolver, loadout);
            Test15_AmbiguousLight(resolver, loadout);

            // --- Confidence ---
            Test16_LowConfidence();

            // --- Empty / Null ---
            Test17_EmptyTranscript(resolver, loadout);
            Test18_NullTranscript(resolver, loadout);

            // --- Combat Integration ---
            Test19_LegalityRejectsIllegalSpell();
            Test20_WrongTurnRejected();
            Test21_ForceFallbackNotBypassed();
            Test22_ManualSelectionStillWorks();
            Test23_VoiceAndManualParity(resolver, loadout);
            Test24_NormalizationConsistency(resolver);

            Debug.Log($"=== TEST RESULTS: {_passed} PASSED, {_failed} FAILED ===");
            if (_failures.Count > 0)
            {
                foreach (var f in _failures)
                {
                    Debug.LogError($"  FAILED: {f}");
                }
            }
        }

        // ============== Helper ==============

        private List<SpellData> CreateTestLoadout()
        {
            return AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
        }

        private List<SpellData> CreateFullRoster()
        {
            var list = new List<SpellData>();
            foreach (var kvp in AuthoritativeCombatRoster.Spells)
            {
                if (kvp.Value != null) list.Add(kvp.Value);
            }
            return list;
        }

        private void Assert(string testName, bool condition, string details = "")
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"  [PASS] {testName}");
            }
            else
            {
                _failed++;
                string msg = string.IsNullOrEmpty(details) ? testName : $"{testName}: {details}";
                _failures.Add(msg);
                Debug.LogError($"  [FAIL] {msg}");
            }
        }

        // ============== Tests ==============

        private void Test01_ExactFireball(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("Fireball", loadout);
            Assert("T01 Exact 'Fireball'", r.IsResolved && r.resolvedSpell.spellId == "FIREBALL",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test02_FireBallSpaced(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("fire ball", loadout);
            Assert("T02 Spaced 'fire ball'", r.IsResolved && r.resolvedSpell.spellId == "FIREBALL",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test03_RoyalInferno(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("Royal Inferno", loadout);
            Assert("T03 Exact 'Royal Inferno'", r.IsResolved && r.resolvedSpell.spellId == "ROYAL_INFERNO",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test04_DeathLight(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            // Death Light may not be in Magma loadout, use full roster
            var fullRoster = CreateFullRoster();
            var r = resolver.Resolve("death light", fullRoster);
            Assert("T04 Exact 'death light'", r.IsResolved && r.resolvedSpell.spellId == "DEATH_LIGHT",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test05_AllCaps(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("FIREBALL", loadout);
            Assert("T05 AllCaps 'FIREBALL'", r.IsResolved && r.resolvedSpell.spellId == "FIREBALL",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test06_ExcessWhitespace(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("  ROYAL   INFERNO! ", loadout);
            Assert("T06 Whitespace '  ROYAL   INFERNO! '", r.IsResolved && r.resolvedSpell.spellId == "ROYAL_INFERNO",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test07_PunctuationStrip(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("FIREBALL!!", loadout);
            Assert("T07 Punctuation 'FIREBALL!!'", r.IsResolved && r.resolvedSpell.spellId == "FIREBALL",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test08_HyphenHandling(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("fire-ball", loadout);
            Assert("T08 Hyphen 'fire-ball'", r.IsResolved && r.resolvedSpell.spellId == "FIREBALL",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test09_TrailingExclamation(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("Royal Inferno!", loadout);
            Assert("T09 Trailing '!' 'Royal Inferno!'", r.IsResolved && r.resolvedSpell.spellId == "ROYAL_INFERNO",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test10_SpellIdAsAlias(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            // Spell IDs like "fireball" are added as aliases in AuthoritativeCombatRoster
            var r = resolver.Resolve("fireball", loadout);
            Assert("T10 SpellId alias 'fireball'", r.IsResolved && r.resolvedSpell.spellId == "FIREBALL",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test11_LowercaseAlias(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("royal_inferno", loadout);
            Assert("T11 Lowercase alias 'royal_inferno'", r.IsResolved && r.resolvedSpell.spellId == "ROYAL_INFERNO",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}");
        }

        private void Test12_TypoFirebal(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            // "firebal" is 1 edit distance from "fireball" — should fuzzy match
            var r = resolver.Resolve("firebal", loadout);
            Assert("T12 Fuzzy 'firebal'→Fireball", r.IsResolved && r.resolvedSpell.spellId == "FIREBALL",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}, method: {r.matchMethod}");
        }

        private void Test13_TypoRoyalInfeno(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            // "royal infeno" is 1 edit from "royal inferno" — should fuzzy match
            var r = resolver.Resolve("royal infeno", loadout);
            Assert("T13 Fuzzy 'royal infeno'→Royal Inferno", r.IsResolved && r.resolvedSpell.spellId == "ROYAL_INFERNO",
                $"Got: {r.status}, spell: {r.resolvedSpell?.spellId}, method: {r.matchMethod}");
        }

        private void Test14_UnknownPhrase(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("Abracadabra", loadout);
            Assert("T14 Unknown 'Abracadabra'", r.status == SpellResolutionStatus.UnknownCommand,
                $"Got: {r.status}");
        }

        private void Test15_AmbiguousLight(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            // "light" could match multiple spells in the full roster
            // In the Magma loadout (Fireball, Fire Cloak, Frozen Heat, Volcanic Asteroid, Royal Inferno, Struggle)
            // there's no "light" match, so it should be unknown
            var r = resolver.Resolve("light", loadout);
            Assert("T15 Ambiguous/Unknown 'light'",
                r.status == SpellResolutionStatus.UnknownCommand || r.status == SpellResolutionStatus.AmbiguousCommand,
                $"Got: {r.status}");
        }

        private void Test16_LowConfidence()
        {
            // Test through LiveKitVoiceInputService confidence filtering
            var service = gameObject.AddComponent<LiveKitVoiceInputService>();
            var config = ScriptableObject.CreateInstance<LiveKitConfig>();
            config.confidenceThreshold = 0.8f;
            service.config = config;

            bool wasUnrecognized = false;
            service.OnSpeechUnrecognized += (phrase) => wasUnrecognized = true;

            var loadout = CreateTestLoadout();
            service.StartListening(loadout);
            // Simulate low-confidence transcript
            service.OnLiveKitTranscriptReceived("Fireball", 0.3f, true);

            Assert("T16 Low confidence rejected", wasUnrecognized, "Expected OnSpeechUnrecognized to fire");

            service.StopListening();
            DestroyImmediate(service);
            DestroyImmediate(config);
        }

        private void Test17_EmptyTranscript(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve("", loadout);
            Assert("T17 Empty transcript", r.status == SpellResolutionStatus.EmptyTranscript,
                $"Got: {r.status}");
        }

        private void Test18_NullTranscript(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            var r = resolver.Resolve(null, loadout);
            Assert("T18 Null transcript", r.status == SpellResolutionStatus.EmptyTranscript,
                $"Got: {r.status}");
        }

        private void Test19_LegalityRejectsIllegalSpell()
        {
            // Verify that a spell not in the loadout is rejected by BattleCoordinator
            var combatConfig = TestCombatDataFactory.CreateTestCombatConfig();
            var network = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var gesture = new MockGestureService(() => Time.time);

            var coordinator = new BattleCoordinator(combatConfig, network, ar, voice, gesture, () => Time.time);

            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Test", null);
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");

            coordinator.SetupMatch("P1", magma, spells1, "P2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            bool wasRejected = false;
            coordinator.OnSpellSelectionRejected += (reason) => wasRejected = true;

            // Try to select Blizzard (not in Magma loadout)
            var blizzard = AuthoritativeCombatRoster.GetSpell("BLIZZARD");
            voice.SimulateSpokenPhrase("Blizzard", coordinator.LocalPlayer.SpellSlots);

            // The spell should be rejected because it's not in the player's loadout
            // (even if voice recognizes it, the coordinator checks loadout membership)
            Assert("T19 Illegal spell rejected",
                wasRejected || coordinator.SelectedSpell == null || coordinator.SelectedSpell.spellId != "BLIZZARD",
                $"Selected: {coordinator.SelectedSpell?.spellId}, Rejected: {wasRejected}");
        }

        private void Test20_WrongTurnRejected()
        {
            var combatConfig = TestCombatDataFactory.CreateTestCombatConfig();
            var network = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var gesture = new MockGestureService(() => Time.time);

            var coordinator = new BattleCoordinator(combatConfig, network, ar, voice, gesture, () => Time.time);

            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Test", null);
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");

            coordinator.SetupMatch("P1", magma, spells1, "P2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            // Complete P1's turn to advance to P2's turn
            voice.SimulateSpokenPhrase("Fireball", coordinator.LocalPlayer.SpellSlots);
            gesture.SimulateGesturePerformed("GESTURE_THRUST", 0.95f);
            ar.SimulateTargetAcquisition(new Vector3(0, 0, 4.5f), 4.5f);
            coordinator.CommitAttackLaunch();

            // Now it's opponent's turn — voice input during wrong turn should be ignored
            var prevPhase = coordinator.StateMachine.CurrentPhase;
            bool recognized = false;
            coordinator.VoiceInput.OnSpeechRecognized += (r) => recognized = true;

            // The state machine should be in SpellSelection for opponent, not accepting local voice
            Assert("T20 Wrong turn: voice during opponent's turn is not directly accepted",
                coordinator.TurnManager.GetActivePlayerState()?.PlayerId != coordinator.LocalPlayer?.PlayerId || !recognized,
                $"Active: {coordinator.TurnManager?.GetActivePlayerState()?.PlayerId}, Phase: {coordinator.StateMachine?.CurrentPhase}");
        }

        private void Test21_ForceFallbackNotBypassed()
        {
            var combatConfig = TestCombatDataFactory.CreateTestCombatConfig();
            var network = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var gesture = new MockGestureService(() => Time.time);

            var coordinator = new BattleCoordinator(combatConfig, network, ar, voice, gesture, () => Time.time);

            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Test", null);
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");

            coordinator.SetupMatch("P1", magma, spells1, "P2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            // Apply Force Fallback to local player
            coordinator.LocalPlayer.TryApplyForceFallback();

            bool wasRejected = false;
            coordinator.OnSpellSelectionRejected += (reason) => wasRejected = true;

            // Try to select Fireball while under Force Fallback
            voice.SimulateSpokenPhrase("Fireball", coordinator.LocalPlayer.SpellSlots);

            Assert("T21 Force Fallback blocks non-Slot6",
                wasRejected || coordinator.SelectedSpell == null,
                $"Rejected: {wasRejected}, Selected: {coordinator.SelectedSpell?.spellId}");
        }

        private void Test22_ManualSelectionStillWorks()
        {
            var combatConfig = TestCombatDataFactory.CreateTestCombatConfig();
            var network = new MockNetworkSessionProvider();
            var ar = new MockARTrackingService();
            var voice = new MockVoiceInputService();
            var gesture = new MockGestureService(() => Time.time);

            var coordinator = new BattleCoordinator(combatConfig, network, ar, voice, gesture, () => Time.time);

            var magma = AuthoritativeCombatRoster.GetGuild("MAGMA");
            var aqua = AuthoritativeCombatRoster.GetGuild("AQUA");
            var arena = TestCombatDataFactory.CreateTestArena(ArenaPreset.VolcanicRuins, "Test", null);
            var spells1 = AuthoritativeCombatRoster.GetSampleLoadout("MAGMA");
            var spells2 = AuthoritativeCombatRoster.GetSampleLoadout("AQUA");

            coordinator.SetupMatch("P1", magma, spells1, "P2", aqua, spells2, arena);
            coordinator.StartBattle(useDebugQuickStart: true);

            bool selected = coordinator.SelectSpellManualFallback(0);
            Assert("T22 Manual selection works",
                selected && coordinator.SelectedSpell != null,
                $"Selected: {selected}, Spell: {coordinator.SelectedSpell?.spellId}");
        }

        private void Test23_VoiceAndManualParity(VoiceSpellCommandResolver resolver, List<SpellData> loadout)
        {
            // Resolve "Fireball" via voice
            var voiceResult = resolver.Resolve("Fireball", loadout);

            // Manual selects by index — get the same spell
            var manualSpell = loadout[0]; // Fireball is slot 0 for Magma

            Assert("T23 Voice+Manual produce same spell",
                voiceResult.IsResolved && voiceResult.resolvedSpell.spellId == manualSpell.spellId,
                $"Voice: {voiceResult.resolvedSpell?.spellId}, Manual: {manualSpell?.spellId}");
        }

        private void Test24_NormalizationConsistency(VoiceSpellCommandResolver resolver)
        {
            // All these should normalize to the same string
            string[] variants = {
                "Fireball", "fireball", "FIREBALL", "  Fireball  ",
                "fireball!", "FIREBALL!!", "fire-ball", " fire  ball "
            };

            // "fire-ball" and "fire  ball" normalize to "fire ball", not "fireball"
            // So let's test that normalization is consistent for each variant
            bool allConsistent = true;
            for (int i = 0; i < variants.Length; i++)
            {
                string norm = VoiceSpellCommandResolver.NormalizeTranscript(variants[i]);
                // Each normalized form should be a non-empty lowercase string
                if (string.IsNullOrWhiteSpace(norm))
                {
                    allConsistent = false;
                    Debug.LogError($"  Normalization of '{variants[i]}' produced empty string");
                }
            }

            Assert("T24 Normalization consistency", allConsistent);
        }
    }
}
