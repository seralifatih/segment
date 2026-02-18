namespace Segment.App.Models
{
    public class AgencyExpansionTriggerResult
    {
        public bool Triggered { get; set; }
        public string Domain { get; set; } = "";
        public int UniqueFreelancerCount { get; set; }
        public PricingPlan SuggestedPlan { get; set; } = PricingPlan.LegalTeam;
        public string Message { get; set; } = "";
    }
}
