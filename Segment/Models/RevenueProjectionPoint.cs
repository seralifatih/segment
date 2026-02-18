namespace Segment.App.Models
{
    public class RevenueProjectionPoint
    {
        public int HorizonMonths { get; set; }
        public decimal ProjectedMrrUsd { get; set; }
        public decimal ProjectedArrUsd { get; set; }
        public decimal GapToTargetArrUsd { get; set; }
        public RevenueRequiredConversions RequiredConversions { get; set; } = new();
    }
}
