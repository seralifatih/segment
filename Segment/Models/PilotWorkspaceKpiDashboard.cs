namespace Segment.App.Models
{
    public class PilotWorkspaceKpiDashboard
    {
        public string WorkspaceId { get; set; } = "";
        public int SeatLimit { get; set; }
        public int InvitedSeatCount { get; set; }
        public int AcceptedSeatCount { get; set; }
        public double SeatUtilizationRate { get; set; }
        public double AverageRetention30d { get; set; }
        public double AverageTrialToPaidConversion { get; set; }
        public double AverageP95LatencyMs { get; set; }
        public double AverageTermViolationRate { get; set; }
        public int TotalSamples { get; set; }
    }
}
