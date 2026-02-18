namespace Segment.App.Models
{
    public class BillingEntitlementSyncResult
    {
        public bool Success { get; set; }
        public bool NoChangesDetected { get; set; }
        public bool InSync { get; set; }
        public string Message { get; set; } = "";
        public BillingEntitlementSyncRecord? Record { get; set; }
    }
}
