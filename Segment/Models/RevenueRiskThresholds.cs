namespace Segment.App.Models
{
    public class RevenueRiskThresholds
    {
        public double ChurnRateThreshold { get; set; } = 0.10;
        public double ConversionRateThreshold { get; set; } = 0.20;
        public double LatencyRegressionThresholdRatio { get; set; } = 0.15;
        public double QualityRegressionThresholdRatio { get; set; } = 0.15;
    }
}
