using System;

namespace Segment.App.Models
{
    public class NicheTelemetryMetricsSnapshot
    {
        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public int TranslationRequestedCount { get; set; }
        public int TranslationCompletedCount { get; set; }
        public int PasteCompletedCount { get; set; }
        public int PasteRevertedCount { get; set; }
        public int GuardrailBlockedCount { get; set; }
        public int GuardrailOverriddenCount { get; set; }
        public int TotalGlossaryHits { get; set; }
        public double WorkflowCompletionRate { get; set; }
        public double GlossaryReuseRate { get; set; }
        public double ViolationRate { get; set; }
        public double P50LatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
    }
}
