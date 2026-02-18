using System;
using LiteDB;

namespace Segment.App.Models
{
    public class GlossaryPackImportRecord
    {
        [BsonId]
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string PackId { get; set; } = "";
        public string ImportedByUserId { get; set; } = "";
        public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
        public string ExportedByUserId { get; set; } = "";
        public string ReferralCode { get; set; } = "";
        public int InsertedTermCount { get; set; }
    }
}
