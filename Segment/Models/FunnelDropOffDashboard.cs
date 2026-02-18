namespace Segment.App.Models
{
    public class FunnelDropOffDashboard
    {
        public int TotalSignups { get; set; }
        public int AcceptedCount { get; set; }
        public int WaitlistCount { get; set; }
        public int RejectedCount { get; set; }
        public double AcceptanceRate { get; set; }
        public double WaitlistRate { get; set; }
        public double RejectionRate { get; set; }
        public double DropOffRate { get; set; }
    }
}
