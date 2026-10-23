using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ArGuildEncounter.Spells;

namespace ArGuildEncounter.Voice
{
    /// <summary>
    /// Resolution result from the VoiceSpellCommandResolver.
    /// </summary>
    public enum SpellResolutionStatus
    {
        Resolved,
        UnknownCommand,
        AmbiguousCommand,
        LowConfidence,
        EmptyTranscript
    }

    /// <summary>
    /// Contains the result of a voice transcript → spell resolution attempt.
    /// </summary>
    public struct SpellResolutionResult
    {
        public SpellResolutionStatus status;
        public SpellData resolvedSpell;
        public string rawTranscript;
        public string normalizedTranscript;
        public string matchMethod; // "exact_name", "exact_alias", "normalized_alias", "fuzzy"
        public float confidence;
        public List<string> ambiguousCandidates;

        public bool IsResolved => status == SpellResolutionStatus.Resolved && resolvedSpell != null;
    }

    /// <summary>
    /// Pure C# resolver that converts raw voice transcripts into canonical SpellData references.
    /// Uses the existing SpellData.displayName and SpellData.voiceAliases as the source of truth.
    /// Does NOT create a duplicate spell list.
    /// 
    /// Matching order:
    /// 1. Exact displayName match (case-insensitive)
    /// 2. Exact voiceAlias match (case-insensitive)
    /// 3. Normalized alias match (strip punctuation from both sides)
    /// 4. Conservative fuzzy match (Levenshtein distance ≤ 2, unique result only)
    /// </summary>
    public class VoiceSpellCommandResolver
    {
        private static readonly Regex _whitespaceCollapse = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex _punctuationStrip = new Regex(@"[^\w\s]", RegexOptions.Compiled);

        /// <summary>
        /// Maximum Levenshtein edit distance for fuzzy matching.
        /// </summary>
        public int MaxFuzzyDistance { get; set; } = 2;

        /// <summary>
        /// Normalizes a raw transcript for comparison:
        /// - lowercase
        /// - trim leading/trailing whitespace
        /// - collapse repeated spaces
        /// - strip punctuation and hyphens
        /// </summary>
        public static string NormalizeTranscript(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            string result = raw.Trim().ToLowerInvariant();

            // Replace hyphens with spaces before stripping punctuation
            result = result.Replace('-', ' ');

            // Strip punctuation (keeps letters, digits, whitespace)
            result = _punctuationStrip.Replace(result, "");

            // Collapse multiple whitespace into single space
            result = _whitespaceCollapse.Replace(result, " ").Trim();

            // Strip conversational prefixes if present (e.g. "cast fireball", "use fireball", "summon fireball")
            if (result.StartsWith("cast ") && result.Length > 5) result = result.Substring(5).Trim();
            else if (result.StartsWith("use ") && result.Length > 4) result = result.Substring(4).Trim();
            else if (result.StartsWith("summon ") && result.Length > 7) result = result.Substring(7).Trim();

            return result;
        }

        /// <summary>
        /// Resolves a raw voice transcript to a canonical SpellData from the active loadout.
        /// Only searches within the provided loadout — does not search the global database.
        /// </summary>
        public SpellResolutionResult Resolve(string rawTranscript, IReadOnlyList<SpellData> activeLoadout, float confidence = 1.0f)
        {
            var result = new SpellResolutionResult
            {
                rawTranscript = rawTranscript ?? "",
                confidence = confidence,
                ambiguousCandidates = new List<string>()
            };

            if (string.IsNullOrWhiteSpace(rawTranscript))
            {
                result.status = SpellResolutionStatus.EmptyTranscript;
                result.normalizedTranscript = "";
                return result;
            }

            string normalized = NormalizeTranscript(rawTranscript);
            result.normalizedTranscript = normalized;

            if (activeLoadout == null || activeLoadout.Count == 0)
            {
                result.status = SpellResolutionStatus.UnknownCommand;
                return result;
            }

            // 1. Exact displayName match
            for (int i = 0; i < activeLoadout.Count; i++)
            {
                var spell = activeLoadout[i];
                if (spell == null) continue;

                if (string.Equals(normalized, NormalizeTranscript(spell.displayName), StringComparison.Ordinal))
                {
                    result.status = SpellResolutionStatus.Resolved;
                    result.resolvedSpell = spell;
                    result.matchMethod = "exact_name";
                    return result;
                }
            }

            // 2. Exact voiceAlias match
            for (int i = 0; i < activeLoadout.Count; i++)
            {
                var spell = activeLoadout[i];
                if (spell == null || spell.voiceAliases == null) continue;

                for (int j = 0; j < spell.voiceAliases.Count; j++)
                {
                    string alias = spell.voiceAliases[j];
                    if (string.IsNullOrWhiteSpace(alias)) continue;

                    if (string.Equals(normalized, alias.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                    {
                        result.status = SpellResolutionStatus.Resolved;
                        result.resolvedSpell = spell;
                        result.matchMethod = "exact_alias";
                        return result;
                    }
                }
            }

            // 3. Normalized alias match (strip punctuation from alias too)
            for (int i = 0; i < activeLoadout.Count; i++)
            {
                var spell = activeLoadout[i];
                if (spell == null || spell.voiceAliases == null) continue;

                for (int j = 0; j < spell.voiceAliases.Count; j++)
                {
                    string alias = spell.voiceAliases[j];
                    if (string.IsNullOrWhiteSpace(alias)) continue;

                    string normalizedAlias = NormalizeTranscript(alias);
                    if (string.Equals(normalized, normalizedAlias, StringComparison.Ordinal))
                    {
                        result.status = SpellResolutionStatus.Resolved;
                        result.resolvedSpell = spell;
                        result.matchMethod = "normalized_alias";
                        return result;
                    }
                }
            }

            // 4. Conservative fuzzy match (Levenshtein distance ≤ MaxFuzzyDistance, unique only)
            SpellData bestFuzzyMatch = null;
            int bestDistance = int.MaxValue;
            int matchCount = 0;

            for (int i = 0; i < activeLoadout.Count; i++)
            {
                var spell = activeLoadout[i];
                if (spell == null) continue;

                // Check against displayName
                string normalizedName = NormalizeTranscript(spell.displayName);
                int dist = LevenshteinDistance(normalized, normalizedName);
                if (dist <= MaxFuzzyDistance)
                {
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestFuzzyMatch = spell;
                        matchCount = 1;
                        result.ambiguousCandidates.Clear();
                        result.ambiguousCandidates.Add(spell.displayName);
                    }
                    else if (dist == bestDistance)
                    {
                        matchCount++;
                        result.ambiguousCandidates.Add(spell.displayName);
                    }
                }

                // Check against aliases
                if (spell.voiceAliases != null)
                {
                    for (int j = 0; j < spell.voiceAliases.Count; j++)
                    {
                        string alias = spell.voiceAliases[j];
                        if (string.IsNullOrWhiteSpace(alias)) continue;

                        string normalizedAlias = NormalizeTranscript(alias);
                        int aDist = LevenshteinDistance(normalized, normalizedAlias);
                        if (aDist <= MaxFuzzyDistance)
                        {
                            if (aDist < bestDistance)
                            {
                                bestDistance = aDist;
                                bestFuzzyMatch = spell;
                                matchCount = 1;
                                result.ambiguousCandidates.Clear();
                                result.ambiguousCandidates.Add(spell.displayName);
                            }
                            else if (aDist == bestDistance && bestFuzzyMatch != spell)
                            {
                                matchCount++;
                                if (!result.ambiguousCandidates.Contains(spell.displayName))
                                    result.ambiguousCandidates.Add(spell.displayName);
                            }
                        }
                    }
                }
            }

            if (matchCount == 1 && bestFuzzyMatch != null)
            {
                result.status = SpellResolutionStatus.Resolved;
                result.resolvedSpell = bestFuzzyMatch;
                result.matchMethod = "fuzzy";
                result.ambiguousCandidates.Clear();
                return result;
            }

            if (matchCount > 1)
            {
                result.status = SpellResolutionStatus.AmbiguousCommand;
                return result;
            }

            // No match found
            result.status = SpellResolutionStatus.UnknownCommand;
            return result;
        }

        /// <summary>
        /// Resolves a transcript against the full AuthoritativeCombatRoster (not just active loadout).
        /// Used for general-purpose spell name recognition.
        /// </summary>
        public SpellResolutionResult ResolveFromRoster(string rawTranscript, float confidence = 1.0f)
        {
            var allSpells = AuthoritativeCombatRoster.Spells;
            var spellList = new List<SpellData>();
            foreach (var kvp in allSpells)
            {
                if (kvp.Value != null)
                    spellList.Add(kvp.Value);
            }
            return Resolve(rawTranscript, spellList, confidence);
        }

        /// <summary>
        /// Computes the Levenshtein edit distance between two strings.
        /// </summary>
        public static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            int lenA = a.Length;
            int lenB = b.Length;

            // Optimization: early exit if length difference exceeds max possible useful distance
            if (Math.Abs(lenA - lenB) > 3) return Math.Abs(lenA - lenB);

            var prev = new int[lenB + 1];
            var curr = new int[lenB + 1];

            for (int j = 0; j <= lenB; j++)
                prev[j] = j;

            for (int i = 1; i <= lenA; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= lenB; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost
                    );
                }
                var temp = prev;
                prev = curr;
                curr = temp;
            }

            return prev[lenB];
        }
    }
}
