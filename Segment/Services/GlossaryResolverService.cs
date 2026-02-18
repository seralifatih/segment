using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class GlossaryResolverService : IGlossaryResolverService
    {
        private static readonly string[] ScopePrecedence = { "Project", "Team", "User", "System" };

        public TermResolutionResult ResolveTerm(string sourceTerm, TermResolutionContext context)
        {
            if (string.IsNullOrWhiteSpace(sourceTerm))
            {
                return new TermResolutionResult
                {
                    Reason = "Source term is empty.",
                    WinningRule = "none",
                    DecisionTrace = new[] { "Input validation failed: source term is empty." }
                };
            }

            var safeContext = context ?? new TermResolutionContext();
            string normalizedInput = Normalize(sourceTerm);
            IReadOnlyList<TermEntry> allTerms = GlossaryService.GetAllTermsForResolution();
            var trace = new List<string>
            {
                $"Rule1: exact source='{normalizedInput}', domain='{safeContext.DomainVertical}', lang='{safeContext.SourceLanguage}->{safeContext.TargetLanguage}'."
            };

            var exactCandidates = allTerms
                .Where(x => IsExactSourceMatch(x, normalizedInput))
                .Where(x => IsDomainMatch(x, safeContext))
                .Where(x => IsLanguagePairMatch(x, safeContext))
                .Where(x => IsOwnerMatch(x, safeContext))
                .ToList();

            trace.Add($"Rule1 result: {exactCandidates.Count} candidates.");

            if (exactCandidates.Count == 0)
            {
                return new TermResolutionResult
                {
                    Reason = "No exact candidate found for source term + language pair + domain.",
                    WinningRule = "none",
                    Candidates = Array.Empty<TermEntry>(),
                    DecisionTrace = trace
                };
            }

            int highestScopeRank = exactCandidates.Max(ComputeScopeRank);
            var scopeCandidates = exactCandidates
                .Where(x => ComputeScopeRank(x) == highestScopeRank)
                .ToList();
            trace.Add($"Rule2: highest scope='{DescribeScopeRank(highestScopeRank)}' with {scopeCandidates.Count} candidate(s).");

            DateTime mostRecentAcceptedAt = scopeCandidates
                .Select(x => x.LastAcceptedAt ?? DateTime.MinValue)
                .Max();

            var recencyCandidates = scopeCandidates
                .Where(x => (x.LastAcceptedAt ?? DateTime.MinValue) == mostRecentAcceptedAt)
                .OrderBy(x => Normalize(x.Target), StringComparer.Ordinal)
                .ThenBy(x => Normalize(x.ScopeOwnerId), StringComparer.Ordinal)
                .ThenBy(x => Normalize(x.Source), StringComparer.Ordinal)
                .ToList();

            trace.Add($"Rule3: most_recent_lastAcceptedAt='{mostRecentAcceptedAt:O}' with {recencyCandidates.Count} candidate(s).");

            if (recencyCandidates.Count > 1)
            {
                trace.Add("Rule4: low-confidence collision detected (same scope and same acceptance recency). UI selection required.");

                var collision = new TermResolutionResult
                {
                    Winner = null,
                    Candidates = scopeCandidates
                        .OrderByDescending(x => x.LastAcceptedAt ?? DateTime.MinValue)
                        .ThenBy(x => Normalize(x.Target), StringComparer.Ordinal)
                        .ThenBy(x => Normalize(x.ScopeOwnerId), StringComparer.Ordinal)
                        .ToList(),
                    Reason = "Low-confidence collision: multiple entries have identical precedence and recency.",
                    WinningRule = "rule4_collision",
                    IsLowConfidenceCollision = true,
                    RequiresUserSelection = true,
                    DecisionTrace = trace
                };

                GlossaryService.RecordResolutionConflict(new GlossaryResolutionConflictRecord
                {
                    CapturedAtUtc = DateTime.UtcNow,
                    SourceTerm = sourceTerm.Trim(),
                    DomainVertical = safeContext.DomainVertical,
                    SourceLanguage = safeContext.SourceLanguage ?? string.Empty,
                    TargetLanguage = safeContext.TargetLanguage ?? string.Empty,
                    CandidateCount = collision.Candidates.Count,
                    WinnerTarget = string.Empty,
                    WinnerScopeType = recencyCandidates[0].ScopeType,
                    WinnerPriority = recencyCandidates[0].Priority,
                    WinnerReason = collision.Reason
                });

                return collision;
            }

            TermEntry winner = recencyCandidates[0];
            trace.Add($"Winner selected deterministically: target='{winner.Target}', scope='{winner.ScopeType}', owner='{winner.ScopeOwnerId}'.");

            if (exactCandidates.Count > 1)
            {
                GlossaryService.RecordResolutionConflict(new GlossaryResolutionConflictRecord
                {
                    CapturedAtUtc = DateTime.UtcNow,
                    SourceTerm = sourceTerm.Trim(),
                    DomainVertical = safeContext.DomainVertical,
                    SourceLanguage = safeContext.SourceLanguage ?? string.Empty,
                    TargetLanguage = safeContext.TargetLanguage ?? string.Empty,
                    CandidateCount = exactCandidates.Count,
                    WinnerTarget = winner.Target,
                    WinnerScopeType = winner.ScopeType,
                    WinnerPriority = winner.Priority,
                    WinnerReason = "Deterministic resolver applied scope/recency tie-break rules."
                });
            }

            return new TermResolutionResult
            {
                Winner = winner,
                Candidates = scopeCandidates
                    .OrderByDescending(x => x.LastAcceptedAt ?? DateTime.MinValue)
                    .ThenBy(x => Normalize(x.Target), StringComparer.Ordinal)
                    .ThenBy(x => Normalize(x.ScopeOwnerId), StringComparer.Ordinal)
                    .ToList(),
                Reason = $"Resolved by deterministic precedence. Scope={winner.ScopeType}, LastAcceptedAt={(winner.LastAcceptedAt ?? DateTime.MinValue):O}.",
                WinningRule = "rule3_recency_after_scope",
                ScopePrecedenceApplied = string.Join(" > ", ScopePrecedence),
                IsLowConfidenceCollision = false,
                RequiresUserSelection = false,
                DecisionTrace = trace
            };
        }

        private static bool IsExactSourceMatch(TermEntry entry, string normalizedInput)
        {
            return string.Equals(Normalize(entry.Source), normalizedInput, StringComparison.Ordinal);
        }

        private static bool IsDomainMatch(TermEntry entry, TermResolutionContext context)
        {
            return entry.DomainVertical == context.DomainVertical;
        }

        private static bool IsLanguagePairMatch(TermEntry entry, TermResolutionContext context)
        {
            bool sourceMatch = string.IsNullOrWhiteSpace(entry.SourceLanguage)
                || string.IsNullOrWhiteSpace(context.SourceLanguage)
                || string.Equals(entry.SourceLanguage, context.SourceLanguage, StringComparison.OrdinalIgnoreCase);
            bool targetMatch = string.IsNullOrWhiteSpace(entry.TargetLanguage)
                || string.IsNullOrWhiteSpace(context.TargetLanguage)
                || string.Equals(entry.TargetLanguage, context.TargetLanguage, StringComparison.OrdinalIgnoreCase);
            return sourceMatch && targetMatch;
        }

        private static bool IsOwnerMatch(TermEntry entry, TermResolutionContext context)
        {
            if (string.IsNullOrWhiteSpace(entry.ScopeOwnerId))
            {
                return true;
            }

            return entry.ScopeType switch
            {
                GlossaryScopeType.Project when !string.IsNullOrWhiteSpace(context.ProjectId)
                    => string.Equals(entry.ScopeOwnerId, context.ProjectId, StringComparison.OrdinalIgnoreCase),
                GlossaryScopeType.Team when !string.IsNullOrWhiteSpace(context.TeamId)
                    => string.Equals(entry.ScopeOwnerId, context.TeamId, StringComparison.OrdinalIgnoreCase),
                GlossaryScopeType.User when !string.IsNullOrWhiteSpace(context.UserId)
                    => string.Equals(entry.ScopeOwnerId, context.UserId, StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }

        private static int ComputeScopeRank(TermEntry entry)
        {
            return entry.ScopeType switch
            {
                GlossaryScopeType.Project => 400,
                GlossaryScopeType.Team => 300,
                GlossaryScopeType.User => 200,
                GlossaryScopeType.System => 100,
                GlossaryScopeType.Session => 50,
                _ => 0
            };
        }

        private static string DescribeScopeRank(int rank)
        {
            return rank switch
            {
                400 => "Project",
                300 => "Team",
                200 => "User",
                100 => "System",
                50 => "Session",
                _ => "Unknown"
            };
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string collapsed = Regex.Replace(value.Trim(), @"\s+", " ");
            return collapsed.ToLowerInvariant();
        }
    }
}
