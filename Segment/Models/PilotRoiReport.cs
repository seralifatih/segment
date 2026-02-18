using System;

namespace Segment.App.Models
{
    public class PilotRoiReport
    {
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public string SessionId { get; set; } = "";
        public double BaselineAverageMinutesPerTask { get; set; }
        public double AssistedAverageMinutesPerTask { get; set; }
        public double TimeSavedPercentage { get; set; }
        public double BaselineViolationRate { get; set; }
        public double AssistedViolationRate { get; set; }
        public double ViolationReductionPercentage { get; set; }
        public double BaselineAcceptanceRate { get; set; }
        public double AssistedAcceptanceRate { get; set; }
        public double AcceptanceRateDelta { get; set; }
        public double BaselineEditRate { get; set; }
        public double AssistedEditRate { get; set; }
        public double EditRateDelta { get; set; }
        public int TotalBaselineSamples { get; set; }
        public int TotalAssistedSamples { get; set; }
        public string ConfidenceSummary { get; set; } = "";
    }
}
