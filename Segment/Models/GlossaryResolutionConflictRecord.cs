using System;
using LiteDB;

namespace Segment.App.Models
{
    public class GlossaryResolutionConflictRecord
    {
        [BsonId]
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
        public string SourceTerm { get; set; } = "";
        public DomainVertical DomainVertical { get; set; } = DomainVertical.Legal;
        public string SourceLanguage { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
        public int CandidateCount { get; set; }
        public string WinnerTarget { get; set; } = "";
        public GlossaryScopeType WinnerScopeType { get; set; } = GlossaryScopeType.Project;
        public int WinnerPriority { get; set; }
        public string WinnerReason { get; set; } = "";
    }
}
