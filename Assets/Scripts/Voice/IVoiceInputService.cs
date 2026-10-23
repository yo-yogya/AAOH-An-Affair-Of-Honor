using System;
using System.Collections.Generic;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.Voice
{
    public struct VoiceRecognitionResult
    {
        public bool isRecognized;
        public string spokenPhrase;
        public SpellData matchedSpell;
        public float confidence;
    }

    /// <summary>
    /// Abstract interface for speech recognition and spell command matching.
    /// Shields the game engine from third-party speech recognition APIs.
    /// </summary>
    public interface IVoiceInputService
    {
        bool IsListening { get; }

        event Action OnListeningStarted;
        event Action OnListeningStopped;
        event Action<VoiceRecognitionResult> OnSpeechRecognized;
        event Action<string> OnSpeechUnrecognized;

        void StartListening(IReadOnlyList<SpellData> activeLoadout);
        void StopListening();
        void SimulateSpokenPhrase(string phrase, IReadOnlyList<SpellData> activeLoadout = null);
        void TriggerManualFallbackSelection(SpellData spell);
    }
}
