using System;
using LiteDB;

namespace Segment.App.Models
{
    public class BillingEntitlementSyncRecord
    {
        [BsonId]
        public string AccountId { get; set; } = "";
        public SubscriptionSelection Selection { get; set; } = new();
        public ResolvedPricingPackage ResolvedPackage { get; set; } = new();
        public string SyncChecksum { get; set; } = "";
        public DateTime LastSyncedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
