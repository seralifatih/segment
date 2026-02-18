using System;
using Segment.App.Models;

namespace Segment.App.Services
{
    public static class TermLearningSafetyEvaluator
    {
        private const double MinConfidence = 0.70;
        private const double MinReputation = 0.60;

        public static TermPromotionAssessment Evaluate(DetectedChange change, double similarityScore)
        {
            if (change == null)
            {
                return new TermPromotionAssessment
                {
                    IsEligible = false,
                    Reason = "Change payload is empty."
                };
            }

            bool rawInstructionLike = PromptSafetySanitizer.IsInstructionLike(change.SourceTerm)
                || PromptSafetySanitizer.IsInstructionLike(change.OldTerm)
                || PromptSafetySanitizer.IsInstructionLike(change.NewTerm);

            string sourceTerm = PromptSafetySanitizer.SanitizeGlossaryConstraint(change.SourceTerm);
            string oldTerm = PromptSafetySanitizer.SanitizeGlossaryConstraint(change.OldTerm);
            string newTerm = PromptSafetySanitizer.SanitizeGlossaryConstraint(change.NewTerm);

            double confidence = ComputeConfidence(similarityScore, oldTerm, newTerm);
            double reputation = Math.Min(
                PromptSafetySanitizer.ComputeReputationScore(sourceTerm),
                PromptSafetySanitizer.ComputeReputationScore(newTerm));

            bool eligible = confidence >= MinConfidence && reputation >= MinReputation
                && !rawInstructionLike
                && !PromptSafetySanitizer.IsInstructionLike(oldTerm)
                && !PromptSafetySanitizer.IsInstructionLike(newTerm);

            return new TermPromotionAssessment
            {
                IsEligible = eligible,
                ConfidenceScore = confidence,
                ReputationScore = reputation,
                Reason = eligible
                    ? "Eligible for suggest-only promotion."
                    : $"Blocked by safety gate (confidence={confidence:F2}, reputation={reputation:F2})."
            };
        }

        private static double ComputeConfidence(double similarityScore, string oldTerm, string newTerm)
        {
            double normalizedSimilarity = Math.Max(0, Math.Min(1, similarityScore));
            int oldLen = string.IsNullOrWhiteSpace(oldTerm) ? 0 : oldTerm.Length;
            int newLen = string.IsNullOrWhiteSpace(newTerm) ? 0 : newTerm.Length;
            int lengthDelta = Math.Abs(oldLen - newLen);
            double lengthPenalty = lengthDelta > 12 ? 0.20 : lengthDelta > 6 ? 0.10 : 0.0;
            double veryShortPenalty = (oldLen < 2 || newLen < 2) ? 0.25 : 0.0;
            return Math.Max(0, Math.Min(1, normalizedSimilarity - lengthPenalty - veryShortPenalty));
        }
    }
}
