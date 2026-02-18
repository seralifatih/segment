using System;

namespace Segment.App.Models
{
    public class GlossaryPackMetadata
    {
        public string PackId { get; set; } = Guid.NewGuid().ToString("N");
        public string PackName { get; set; } = "Legal Glossary Pack";
        public string ExportedByUserId { get; set; } = "";
        public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
        public string ReferralCode { get; set; } = "";
    }
}
