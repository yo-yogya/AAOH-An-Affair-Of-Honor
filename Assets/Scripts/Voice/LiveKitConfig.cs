using UnityEngine;

namespace ArGuildEncounter.Voice
{
    /// <summary>
    /// ScriptableObject configuration for the LiveKit/Seeway voice recognition service.
    /// Stores connection credentials, recognition thresholds, and behavior settings.
    /// Create via: Assets > Create > AR Guild Encounter > LiveKit Config
    /// </summary>
    [CreateAssetMenu(fileName = "LiveKitConfig", menuName = "AR Guild Encounter/LiveKit Config")]
    public class LiveKitConfig : ScriptableObject
    {
        [Header("LiveKit Cloud Connection")]
        [Tooltip("WebSocket URL for the LiveKit cloud instance.")]
        public string livekitUrl = "wss://see-way-gl0rgqf9.livekit.cloud";

        [Tooltip("LiveKit API Key for authentication.")]
        public string apiKey = "APIGHQ3XyHR2qSH";

        [Tooltip("LiveKit API Secret for JWT token generation.")]
        public string apiSecret = "etIjtmknc8VHpxCvM0GyqfbukRHwXujh1BVeOaHIyHpB";

        [Header("Recognition Settings")]
        [Tooltip("Minimum confidence threshold to accept a recognition result (0.0–1.0).")]
        [Range(0.0f, 1.0f)]
        public float confidenceThreshold = 0.6f;

        [Tooltip("Maximum time in seconds to listen for speech before timing out.")]
        public float listenTimeoutSeconds = 8.0f;

        [Tooltip("If true, partial transcripts are forwarded for UI display (but never used for spell selection).")]
        public bool showPartialTranscripts = false;

        [Header("Fuzzy Matching")]
        [Tooltip("Maximum Levenshtein edit distance for fuzzy spell name matching.")]
        [Range(1, 4)]
        public int maxFuzzyEditDistance = 2;

        [Header("Microphone Settings")]
        [Tooltip("Audio sample rate in Hz for microphone capture.")]
        public int sampleRate = 16000;

        [Tooltip("Audio buffer length in seconds for microphone capture.")]
        public int micBufferLengthSeconds = 1;

        [Header("Debug")]
        [Tooltip("Enable verbose logging of voice recognition events.")]
        public bool enableDebugLogging = true;

        /// <summary>
        /// Loads the LiveKitConfig from Resources. Returns null if not found.
        /// </summary>
        public static LiveKitConfig LoadFromResources()
        {
            return Resources.Load<LiveKitConfig>("LiveKitConfig");
        }
    }
}
