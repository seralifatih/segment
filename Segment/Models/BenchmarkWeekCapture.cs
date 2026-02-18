using System.Collections.Generic;

namespace Segment.App.Models
{
    public class BenchmarkWeekCapture
    {
        public int WeekNumber { get; set; }
        public BenchmarkPeriodType PeriodType { get; set; } = BenchmarkPeriodType.Baseline;
        public List<BenchmarkSegmentMetric> SegmentMetrics { get; set; } = new();
    }
}
