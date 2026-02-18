using System;
using System.Collections.Generic;
using LiteDB;

namespace Segment.App.Models
{
    public class BenchmarkSession
    {
        [BsonId]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string PilotName { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsCompleted { get; set; }
        public List<BenchmarkWeekCapture> WeekCaptures { get; set; } = new();
        public PilotRoiReport? FinalReport { get; set; }
    }
}
