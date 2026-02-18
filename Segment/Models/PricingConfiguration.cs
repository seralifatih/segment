using System.Collections.Generic;

namespace Segment.App.Models
{
    public class PricingConfiguration
    {
        public bool PlatformFeeEnabled { get; set; }
        public decimal MonthlyPlatformFee { get; set; }
        public decimal AnnualPlatformFee { get; set; }
        public List<PlanPriceRule> PriceRules { get; set; } = new();
    }
}
