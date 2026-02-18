using System;
using System.Collections.Generic;
using LiteDB;

namespace Segment.App.Models
{
    public class AccountMetadata
    {
        [BsonId]
        public string AccountId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public List<string> PartnerTags { get; set; } = new();
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
