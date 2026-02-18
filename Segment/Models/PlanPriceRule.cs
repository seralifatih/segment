namespace Segment.App.Models
{
    public class PlanPriceRule
    {
        public PricingPlan Plan { get; set; } = PricingPlan.LegalProIndividual;
        public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
        public decimal BasePrice { get; set; }
        public bool SeatBased { get; set; }
        public decimal SeatPrice { get; set; }
        public int MinimumSeats { get; set; } = 1;
        public bool SupportsPlatformFee { get; set; }
    }
}
