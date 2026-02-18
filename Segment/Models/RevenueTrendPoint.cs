using System;

namespace Segment.App.Models
{
    public class RevenueTrendPoint
    {
        public DateTime CapturedAtUtc { get; set; }
        public int ActivePaidFreelancers { get; set; }
        public int AgencySeats { get; set; }
        public int EnterpriseAddOns { get; set; }
        public decimal MrrUsd { get; set; }
        public decimal ArrUsd { get; set; }
    }
}
