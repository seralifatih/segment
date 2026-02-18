using System;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class LearningConsentService : ILearningConsentService
    {
        public LearningConsentOutcome ApplyDecision(
            DetectedChange change,
            LearningConsentOption option,
            Func<LearningConflictPrompt, LearningConflictDecision>? conflictResolver = null)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));

            if (option == LearningConsentOption.NotNow)
            {
                return new LearningConsentOutcome
                {
                    Skipped = true,
                    Reason = "User deferred learning decision."
                };
            }

            string source = PromptSafetySanitizer.SanitizeGlossaryConstraint(change.SourceTerm);
            string target = PromptSafetySanitizer.SanitizeGlossaryConstraint(change.NewTerm);
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            {
                return new LearningConsentOutcome
                {
                    Skipped = true,
                    Reason = "Term candidate is empty after sanitization."
                };
            }

            bool isGlobal = option == LearningConsentOption.Always;
            var profile = isGlobal ? GlossaryService.GlobalProfile : GlossaryService.CurrentProfile;
            var existing = profile.Terms.FindById(source);

            if (existing != null && string.Equals(existing.Target, target, StringComparison.OrdinalIgnoreCase))
            {
                return new LearningConsentOutcome
                {
                    Saved = true,
                    IsGlobalScope = isGlobal,
                    Reason = "Same preferred translation already exists."
                };
            }

            if (existing != null)
            {
                if (conflictResolver == null)
                {
                    RecordConflict(source, existing, string.Empty, "learning_conflict_unresolved");
                    return new LearningConsentOutcome
                    {
                        RequiresConflictResolution = true,
                        IsGlobalScope = isGlobal,
                        Reason = "Conflict detected; explicit user selection required."
                    };
                }

                var decision = conflictResolver(new LearningConflictPrompt
                {
                    SourceTerm = source,
                    ExistingTarget = existing.Target,
                    NewTarget = target,
                    IsGlobalScope = isGlobal,
                    CapturedAtUtc = DateTime.UtcNow
                });

                if (decision == LearningConflictDecision.Cancel)
                {
                    RecordConflict(source, existing, string.Empty, "learning_conflict_unresolved");
                    return new LearningConsentOutcome
                    {
                        Skipped = true,
                        IsGlobalScope = isGlobal,
                        Reason = "User cancelled conflict resolution."
                    };
                }

                if (decision == LearningConflictDecision.KeepExisting)
                {
                    RecordConflict(source, existing, existing.Target, "learning_conflict_keep_existing");
                    GlossaryService.RecordUsage(new TermUsageLogRecord
                    {
                        CapturedAtUtc = DateTime.UtcNow,
                        ScopeName = profile.Name,
                        Source = source,
                        Action = "learning_conflict_keep_existing",
                        Success = true,
                        Metadata = "{}"
                    });

                    return new LearningConsentOutcome
                    {
                        Saved = false,
                        IsGlobalScope = isGlobal,
                        Reason = "Existing translation preserved by user choice."
                    };
                }

                RecordConflict(source, existing, target, "learning_conflict_use_new");
            }

            GlossaryService.AddTerm(source, target, isGlobal);
            GlossaryService.RecordUsage(new TermUsageLogRecord
            {
                CapturedAtUtc = DateTime.UtcNow,
                ScopeName = profile.Name,
                Source = source,
                Action = isGlobal ? "learning_saved_global" : "learning_saved_project",
                Success = true,
                Metadata = "{}"
            });

            return new LearningConsentOutcome
            {
                Saved = true,
                IsGlobalScope = isGlobal,
                ConflictResolvedWithOverwrite = existing != null,
                Reason = existing == null
                    ? "Preferred translation saved."
                    : "Conflict resolved by using new suggested translation."
            };
        }

        private static void RecordConflict(string source, TermEntry existing, string winnerTarget, string reason)
        {
            GlossaryService.RecordResolutionConflict(new GlossaryResolutionConflictRecord
            {
                CapturedAtUtc = DateTime.UtcNow,
                SourceTerm = source,
                DomainVertical = existing.DomainVertical,
                SourceLanguage = existing.SourceLanguage,
                TargetLanguage = existing.TargetLanguage,
                CandidateCount = 2,
                WinnerTarget = winnerTarget,
                WinnerScopeType = existing.ScopeType,
                WinnerPriority = existing.Priority,
                WinnerReason = reason
            });
        }
    }
}
