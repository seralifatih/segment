using System;

namespace Segment.App.Models
{
    public class LearningConflictPrompt
    {
        public string SourceTerm { get; set; } = string.Empty;
        public string ExistingTarget { get; set; } = string.Empty;
        public string NewTarget { get; set; } = string.Empty;
        public bool IsGlobalScope { get; set; }
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
