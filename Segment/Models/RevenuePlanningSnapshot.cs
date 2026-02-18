using System.Collections.Generic;

namespace Segment.App.Models
{
    public class RevenuePlanningSnapshot
    {
        public ArrScenarioType ScenarioType { get; set; } = ArrScenarioType.Mixed;
        public decimal TargetArrUsd { get; set; }

        public int PlannedActivePaidFreelancers { get; set; }
        public int PlannedAgencySeats { get; set; }
        public int PlannedEnterpriseAddOns { get; set; }

        public int ActualActivePaidFreelancers { get; set; }
        public int ActualAgencySeats { get; set; }
        public int ActualEnterpriseAddOns { get; set; }

        public decimal PlannedMrrUsd { get; set; }
        public decimal PlannedArrUsd { get; set; }
        public decimal ActualMrrUsd { get; set; }
        public decimal ActualArrUsd { get; set; }
        public decimal ArrGapUsd { get; set; }

        public List<RevenueTrendPoint> Trend { get; set; } = new();
        public List<RevenueProjectionPoint> Projections { get; set; } = new();
        public RevenueRiskAssessment Risks { get; set; } = new();
    }
}
