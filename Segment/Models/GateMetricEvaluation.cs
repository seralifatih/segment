namespace Segment.App.Models
{
    public class GateMetricEvaluation
    {
        public string MetricKey { get; set; } = "";
        public double ActualValue { get; set; }
        public double Threshold { get; set; }
        public KpiComparisonOperator ComparisonOperator { get; set; }
        public bool Passed { get; set; }
    }
}
