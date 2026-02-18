using System;

namespace Segment.App.Models
{
    public class PilotWorkspaceKpiSample
    {
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
        public double Retention30d { get; set; }
        public double TrialToPaidConversion { get; set; }
        public double P95LatencyMs { get; set; }
        public double TermViolationRate { get; set; }
        public int ActiveSeatCount { get; set; }
    }
}
