using System;
using System.Collections.Generic;

namespace Segment.App.Models
{
    public class ComplianceAuditRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
        public ComplianceAuditEventType EventType { get; set; }
        public string AccountId { get; set; } = "";
        public string Decision { get; set; } = "";
        public string ActiveMode { get; set; } = "";
        public string ProviderRoute { get; set; } = "";
        public string RetentionPolicySummary { get; set; } = "";
        public string Details { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
