using System.Collections.Generic;

namespace Segment.App.Models
{
    public class PricingPlanDefinition
    {
        public string PlanId { get; set; } = "";
        public CustomerSegment Segment { get; set; } = CustomerSegment.FreelancerLegal;
        public decimal MonthlyPrice { get; set; }
        public List<string> Entitlements { get; set; } = new();
    }
}
