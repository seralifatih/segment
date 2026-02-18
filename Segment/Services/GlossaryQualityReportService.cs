using System;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class GlossaryQualityReportService : IGlossaryQualityReportService
    {
        public GlossaryQualityReport BuildReport(string workspaceId)
        {
            var terms = GlossaryService.GetEffectiveTerms().Values.ToList();
            int total = terms.Count;
            int confirmed = terms.Count(x => x.IsUserConfirmed);
            int recentlyUsed = terms.Count(x => x.LastUsed >= DateTime.Now.AddDays(-30));

            double confirmationRate = total == 0 ? 0 : (double)confirmed / total;
            double recentUsageRate = total == 0 ? 0 : (double)recentlyUsed / total;
            double estimatedViolationRate = total == 0 ? 0.05 : Math.Max(0, 1 - confirmationRate) * 0.08;

            return new GlossaryQualityReport
            {
                WorkspaceId = workspaceId ?? "",
                TotalTerms = total,
                ConfirmedTerms = confirmed,
                RecentlyUsedTerms = recentlyUsed,
                ConfirmationRate = confirmationRate,
                RecentUsageRate = recentUsageRate,
                EstimatedViolationRate = estimatedViolationRate
            };
        }
    }
}
