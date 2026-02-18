using System;

namespace Segment.App.Models
{
    public class RevenueActualSnapshot
    {
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
        public int ActivePaidFreelancers { get; set; }
        public int AgencySeats { get; set; }
        public int EnterpriseAddOns { get; set; }
    }
}
