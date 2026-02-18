namespace Segment.App.Models
{
    public class GtmKpiTarget
    {
        public string MetricKey { get; set; } = "";
        public double Threshold { get; set; }
        public KpiComparisonOperator ComparisonOperator { get; set; } = KpiComparisonOperator.GreaterThanOrEqual;
        public LaunchPhase Phase { get; set; } = LaunchPhase.Phase0Readiness;
    }
}
