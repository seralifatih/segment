using System.Collections.Generic;

namespace Segment.App.Models
{
    public class PlanTransitionResult
    {
        public bool Allowed { get; set; }
        public string Reason { get; set; } = "";
        public SubscriptionSelection UpdatedSelection { get; set; } = new();
        public IReadOnlyList<PricingPlan> UpgradePaths { get; set; } = new List<PricingPlan>();
    }
}
