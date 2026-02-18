namespace Segment.App.Models
{
    public class PilotConversionFunnelDashboard
    {
        public int WeeklyPilotActiveUsers { get; set; }
        public double LatestPilotToPaidConversion { get; set; }
        public double TwelveWeekAveragePilotToPaidConversion { get; set; }
        public double LatestChurnRate { get; set; }
        public double TwelveWeekAverageChurnRate { get; set; }
    }
}
