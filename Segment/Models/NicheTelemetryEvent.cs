using System;
using LiteDB;

namespace Segment.App.Models
{
    public class NicheTelemetryEvent
    {
        [BsonId]
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string IngestionKey { get; set; } = "";
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
        public NicheTelemetryEventType EventType { get; set; } = NicheTelemetryEventType.TranslationRequested;
        public DomainVertical DomainVertical { get; set; } = DomainVertical.Legal;
        public double LatencyMs { get; set; }
        public bool Success { get; set; }
        public int BlockedCount { get; set; }
        public int OverrideCount { get; set; }
        public int GlossaryHitCount { get; set; }
        public string SegmentHash { get; set; } = "";
    }
}
