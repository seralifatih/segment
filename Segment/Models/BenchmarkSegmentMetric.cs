namespace Segment.App.Models
{
    public class BenchmarkSegmentMetric
    {
        public CustomerSegment Segment { get; set; } = CustomerSegment.FreelancerLegal;
        public int SampleCount { get; set; }
        public double AverageMinutesPerTask { get; set; }
        public int TerminologyViolationCount { get; set; }
        public int AcceptanceCount { get; set; }
        public int EditCount { get; set; }
    }
}
