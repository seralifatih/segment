using System.Collections.Generic;

namespace Segment.App.Models
{
    public class RevenueRiskAssessment
    {
        public bool ChurnAboveThreshold { get; set; }
        public bool ConversionBelowThreshold { get; set; }
        public bool LatencyRegressionRisk { get; set; }
        public bool QualityRegressionRisk { get; set; }
        public List<string> Alerts { get; set; } = new();
    }
}
