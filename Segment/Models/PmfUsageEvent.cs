using System;
using LiteDB;

namespace Segment.App.Models
{
    public class PmfUsageEvent
    {
        [BsonId]
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string IngestionKey { get; set; } = "";
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
        public string UserIdHash { get; set; } = "";
        public CustomerSegment Segment { get; set; } = CustomerSegment.FreelancerLegal;
        public int SegmentsCompleted { get; set; }
        public int GlossarySuggestionsServed { get; set; }
        public int GlossarySuggestionsAccepted { get; set; }
        public int TerminologyViolationCount { get; set; }
        public double LatencyMs { get; set; }
        public bool IsPilotUser { get; set; }
        public bool ConvertedToPaid { get; set; }
        public bool Churned { get; set; }
    }
}
