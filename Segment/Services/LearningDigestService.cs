using System;
using System.Globalization;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class LearningDigestService : ILearningDigestService
    {
        public WeeklyLearningDigest BuildWeeklyDigest(DateTime? utcNow = null)
        {
            DateTime end = (utcNow ?? DateTime.UtcNow).ToUniversalTime();
            DateTime start = end.AddDays(-7);

            var usage = GlossaryService.GetUsageLogs(5000)
                .Where(x => x.CapturedAtUtc >= start && x.CapturedAtUtc <= end)
                .ToList();

            var learned = usage
                .Where(x => x.Success && (x.Action == "learning_saved_global" || x.Action == "learning_saved_project"))
                .ToList();

            var unresolved = GlossaryService.GetResolutionConflicts()
                .Where(x => x.CapturedAtUtc >= start && x.CapturedAtUtc <= end)
                .Count(x => string.IsNullOrWhiteSpace(x.WinnerTarget)
                    || x.WinnerReason.Contains("unresolved", StringComparison.OrdinalIgnoreCase)
                    || x.WinnerReason.Contains("collision", StringComparison.OrdinalIgnoreCase));

            return new WeeklyLearningDigest
            {
                WindowStartUtc = start,
                WindowEndUtc = end,
                TermsLearned = learned.Count,
                UnresolvedConflicts = unresolved,
                LearnedTerms = learned
                    .Select(x => x.Source)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToList()
            };
        }
    }
}
