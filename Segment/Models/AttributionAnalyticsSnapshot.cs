using System.Collections.Generic;

namespace Segment.App.Models
{
    public class AttributionAnalyticsSnapshot
    {
        public int TotalGlossaryPackImports { get; set; }
        public int TotalReferralRewardsGranted { get; set; }
        public Dictionary<string, int> ImportsByReferralCode { get; set; } = new();
    }
}
