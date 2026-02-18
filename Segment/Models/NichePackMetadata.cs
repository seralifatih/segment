using System;

namespace Segment.App.Models
{
    public class NichePackMetadata
    {
        public string PackId { get; set; } = Guid.NewGuid().ToString("N");
        public string PackName { get; set; } = "Niche Pack";
        public string ExportedByUserId { get; set; } = "";
        public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
