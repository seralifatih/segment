namespace Segment.App.Models
{
    public class ArrScenarioPlan
    {
        public ArrScenarioType ScenarioType { get; set; } = ArrScenarioType.Mixed;
        public decimal TargetArrUsd { get; set; } = 1_000_000m;
        public RevenuePricingAssumptions Pricing { get; set; } = new();

        public int PlannedActivePaidFreelancers { get; set; }
        public int PlannedAgencySeats { get; set; }
        public int PlannedEnterpriseAddOns { get; set; }

        public double MonthlyFreelancerGrowthRate { get; set; } = 0.05;
        public double MonthlyAgencySeatGrowthRate { get; set; } = 0.06;
        public double MonthlyEnterpriseAddOnGrowthRate { get; set; } = 0.04;
    }
}
