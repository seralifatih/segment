using System;

namespace Segment.App.Models
{
    public class ReflexLatencySnapshot
    {
        public DateTime WindowComputedAtUtc { get; set; } = DateTime.UtcNow;
        public int SampleCount { get; set; }
        public int ShortSegmentSampleCount { get; set; }

        public double CaptureToRequestStartP50Ms { get; set; }
        public double CaptureToRequestStartP95Ms { get; set; }
        public double ProviderRoundtripP50Ms { get; set; }
        public double ProviderRoundtripP95Ms { get; set; }
        public double ResponseToRenderP50Ms { get; set; }
        public double ResponseToRenderP95Ms { get; set; }
        public double EndToEndP50Ms { get; set; }
        public double EndToEndP95Ms { get; set; }
    }
}
