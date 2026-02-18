namespace Segment.App.Models
{
    public class ResolvedPricingPackage
    {
        public PricingPlan Plan { get; set; } = PricingPlan.LegalProIndividual;
        public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
        public int EffectiveSeats { get; set; } = 1;
        public decimal Subtotal { get; set; }
        public decimal PlatformFee { get; set; }
        public decimal Total { get; set; }
        public PlanEntitlements Entitlements { get; set; } = new();
    }
}
