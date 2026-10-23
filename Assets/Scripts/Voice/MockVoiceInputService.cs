using System;
using System.Collections.Generic;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.Voice
{
    /// <summary>
    /// Mock implementation of IVoiceInputService.
    /// Routes through VoiceSpellCommandResolver for full normalization, alias, and fuzzy matching
    /// parity with the production LiveKit service. Falls back to direct MatchesVoiceAlias if resolver
    /// is unavailable.
    /// </summary>
    public class MockVoiceInputService : IVoiceInputService
    {
        private bool _isListening = false;
        private IReadOnlyList<SpellData> _currentLoadout;
        private readonly VoiceSpellCommandResolver _resolver = new VoiceSpellCommandResolver();

        // Debug accessors (mirror LiveKitVoiceInputService)
        public string LastRawTranscript { get; private set; } = "";
        public string LastNormalizedTranscript { get; private set; } = "";
        public string LastMatchedSpellName { get; private set; } = "";
        public string LastResolutionResult { get; private set; } = "";

        public bool IsListening => _isListening;

        public event Action OnListeningStarted;
        public event Action OnListeningStopped;
        public event Action<VoiceRecognitionResult> OnSpeechRecognized;
        public event Action<string> OnSpeechUnrecognized;

        public void StartListening(IReadOnlyList<SpellData> activeLoadout)
        {
            _isListening = true;
            _currentLoadout = activeLoadout;
            OnListeningStarted?.Invoke();
        }

        public void StopListening()
        {
            _isListening = false;
            OnListeningStopped?.Invoke();
        }

        public void SimulateSpokenPhrase(string phrase, IReadOnlyList<SpellData> activeLoadout = null)
        {
            var loadout = activeLoadout ?? _currentLoadout;
            LastRawTranscript = phrase ?? "";

            if (loadout == null || string.IsNullOrWhiteSpace(phrase))
            {
                LastResolutionResult = "Empty/null";
                OnSpeechUnrecognized?.Invoke(phrase);
                return;
            }

            // Route through VoiceSpellCommandResolver for full normalization + fuzzy matching
            var resolution = _resolver.Resolve(phrase, loadout, 0.95f);
            LastNormalizedTranscript = resolution.normalizedTranscript;

            if (resolution.IsResolved)
            {
                LastMatchedSpellName = resolution.resolvedSpell.displayName;
                LastResolutionResult = $"Resolved ({resolution.matchMethod})";

                var result = new VoiceRecognitionResult
                {
                    isRecognized = true,
                    spokenPhrase = phrase,
                    matchedSpell = resolution.resolvedSpell,
                    confidence = 0.95f
                };
                OnSpeechRecognized?.Invoke(result);
            }
            else
            {
                LastMatchedSpellName = "";
                LastResolutionResult = resolution.status.ToString();

                // Fallback: try direct MatchesVoiceAlias for backward compatibility
                SpellData fallbackMatch = null;
                for (int i = 0; i < loadout.Count; i++)
                {
                    var spell = loadout[i];
                    if ((object)spell != null && spell.MatchesVoiceAlias(phrase))
                    {
                        fallbackMatch = spell;
                        break;
                    }
                }

                if ((object)fallbackMatch != null)
                {
                    LastMatchedSpellName = fallbackMatch.displayName;
                    LastResolutionResult = "Resolved (legacy_alias)";
                    var result = new VoiceRecognitionResult
                    {
                        isRecognized = true,
                        spokenPhrase = phrase,
                        matchedSpell = fallbackMatch,
                        confidence = 0.95f
                    };
                    OnSpeechRecognized?.Invoke(result);
                }
                else
                {
                    OnSpeechUnrecognized?.Invoke(phrase);
                }
            }
        }

        public void TriggerManualFallbackSelection(SpellData spell)
        {
            if ((object)spell != null)
            {
                var result = new VoiceRecognitionResult
                {
                    isRecognized = true,
                    spokenPhrase = $"[Manual Tap] {spell.displayName}",
                    matchedSpell = spell,
                    confidence = 1.0f
                };
                OnSpeechRecognized?.Invoke(result);
            }
        }
    }
}
