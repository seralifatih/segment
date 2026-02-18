using System;

namespace Segment.App.Models
{
    public class ReflexLatencySample
    {
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsShortSegment { get; set; }
        public int SourceLength { get; set; }
        public double CaptureToRequestStartMs { get; set; }
        public double ProviderRoundtripMs { get; set; }
        public double ResponseToRenderMs { get; set; }
        public double EndToEndMs { get; set; }
        public string ProviderUsed { get; set; } = "";
        public bool UsedFallbackProvider { get; set; }
        public bool BudgetEnforced { get; set; }
        public bool BudgetExceeded { get; set; }
    }
}
