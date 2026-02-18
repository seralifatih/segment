namespace Segment.App.Models
{
    public class GrowthObservabilityDashboard
    {
        public FunnelDropOffDashboard FunnelDropOff { get; set; } = new();
        public ReferralConversionFunnelDashboard ReferralConversion { get; set; } = new();
        public PilotConversionFunnelDashboard PilotConversion { get; set; } = new();
    }
}
