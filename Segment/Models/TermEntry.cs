using System;
using LiteDB;


namespace Segment.App.Models
{
    public class TermEntry
    {
        // Temel Veriler
        [BsonId]
        public string Source { get; set; } = "";     // agreement (Lemma)
        public string Target { get; set; } = "";     // sözleşme (Lemma)

        // Metadata (Gelecek için yatırım) 💎
        public string Context { get; set; } = "";    // "legal", "general" vs.
        public string Pos { get; set; } = "";        // "noun", "verb"
        public string CreatedBy { get; set; } = "user";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUsed { get; set; } = DateTime.Now;
        public int UsageCount { get; set; } = 0;
        public bool IsUserConfirmed { get; set; } = true;

        // Domain-aware layering metadata
        public DomainVertical DomainVertical { get; set; } = DomainVertical.Legal;
        public string SourceLanguage { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
        public GlossaryScopeType ScopeType { get; set; } = GlossaryScopeType.Project;
        public string ScopeOwnerId { get; set; } = "";
        public int Priority { get; set; }
        public DateTime? LastAcceptedAt { get; set; }
    }
}
