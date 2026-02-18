namespace Segment.App.Models
{
    public class SubscriptionSelection
    {
        public PricingPlan Plan { get; set; } = PricingPlan.LegalProIndividual;
        public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
        public int Seats { get; set; } = 1;
        public bool ApplyPlatformFee { get; set; }
    }
}
