using System;

namespace Segment.App.Models
{
    public class BillingEntitlementSyncRequest
    {
        public string AccountId { get; set; } = "";
        public SubscriptionSelection Selection { get; set; } = new();
        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
