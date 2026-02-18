using System;

namespace Segment.App.Models
{
    public class TermUsageLogRecord
    {
        public long Id { get; set; }
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
        public string ScopeName { get; set; } = "";
        public string Source { get; set; } = "";
        public string Action { get; set; } = "";
        public bool Success { get; set; }
        public string Metadata { get; set; } = "";
    }
}
