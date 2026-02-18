using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IBillingEntitlementSyncService
    {
        BillingEntitlementSyncResult Sync(BillingEntitlementSyncRequest request);
        BillingEntitlementSyncRecord? GetLatest(string accountId);
    }
}
