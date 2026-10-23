using System;
using System.Text;
using UnityEngine;

namespace ArGuildEncounter.Voice
{
    /// <summary>
    /// Generates LiveKit-compatible JWT access tokens for joining a LiveKit room from Unity.
    /// Uses HMAC-SHA256 signing with the API secret from LiveKitConfig.
    /// This is a lightweight local JWT generator — no server roundtrip needed for development.
    /// </summary>
    public static class LiveKitTokenGenerator
    {
        /// <summary>
        /// Generates a JWT access token for joining a LiveKit room.
        /// </summary>
        /// <param name="config">LiveKit configuration with API key and secret.</param>
        /// <param name="roomName">The room name to join.</param>
        /// <param name="participantIdentity">Unique identity for this participant.</param>
        /// <param name="ttlSeconds">Token time-to-live in seconds (default: 3600 = 1 hour).</param>
        /// <returns>A signed JWT token string, or null if generation fails.</returns>
        public static string GenerateToken(LiveKitConfig config, string roomName, string participantIdentity, int ttlSeconds = 3600)
        {
            if (config == null || string.IsNullOrEmpty(config.apiKey) || string.IsNullOrEmpty(config.apiSecret))
            {
                Debug.LogError("[LiveKitTokenGenerator] Cannot generate token: missing API key or secret in LiveKitConfig.");
                return null;
            }

            try
            {
                long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long expUnix = nowUnix + ttlSeconds;

                // JWT Header
                string header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";

                // JWT Payload — LiveKit access token claims
                // See: https://docs.livekit.io/home/get-started/authentication/
                string payload = "{"
                    + $"\"iss\":\"{EscapeJson(config.apiKey)}\","
                    + $"\"sub\":\"{EscapeJson(participantIdentity)}\","
                    + $"\"iat\":{nowUnix},"
                    + $"\"exp\":{expUnix},"
                    + $"\"nbf\":{nowUnix},"
                    + $"\"jti\":\"{Guid.NewGuid():N}\","
                    + "\"video\":{"
                    + $"\"roomJoin\":true,"
                    + $"\"room\":\"{EscapeJson(roomName)}\","
                    + "\"canPublish\":true,"
                    + "\"canSubscribe\":true,"
                    + "\"canPublishData\":true"
                    + "}"
                    + "}";

                string headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
                string payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
                string signingInput = headerB64 + "." + payloadB64;

                byte[] keyBytes = Encoding.UTF8.GetBytes(config.apiSecret);
                byte[] inputBytes = Encoding.UTF8.GetBytes(signingInput);

                byte[] hash;
                using (var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes))
                {
                    hash = hmac.ComputeHash(inputBytes);
                }

                string signatureB64 = Base64UrlEncode(hash);
                string token = signingInput + "." + signatureB64;

                if (config.enableDebugLogging)
                {
                    Debug.Log($"[LiveKitTokenGenerator] Generated token for room '{roomName}', identity '{participantIdentity}', expires in {ttlSeconds}s.");
                }

                return token;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LiveKitTokenGenerator] Token generation failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generates a token for the spell recognition room.
        /// Uses a fixed room name "spell-recognition" by default.
        /// </summary>
        public static string GenerateSpellRecognitionToken(LiveKitConfig config, string playerIdentity = null)
        {
            string identity = playerIdentity ?? $"unity-player-{SystemInfo.deviceUniqueIdentifier.Substring(0, 8)}";
            return GenerateToken(config, "spell-recognition", identity);
        }

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
