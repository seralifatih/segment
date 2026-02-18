using System;
using System.Globalization;
using System.IO;
using System.Threading;
using LiteDB;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class BillingEntitlementSyncService : IBillingEntitlementSyncService, IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<BillingEntitlementSyncRecord> _records;
        private readonly IPricingEngineService _pricingEngineService;

        public BillingEntitlementSyncService(IPricingEngineService? pricingEngineService = null, string? basePath = null)
        {
            _pricingEngineService = pricingEngineService ?? new PricingEngineService();
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");

            Directory.CreateDirectory(resolvedBasePath);
            string dbPath = Path.Combine(resolvedBasePath, "billing_entitlement_sync.db");

            _database = new LiteDatabase(dbPath);
            _records = _database.GetCollection<BillingEntitlementSyncRecord>("billing_entitlement_sync");
            _records.EnsureIndex(x => x.AccountId, unique: true);
            _records.EnsureIndex(x => x.LastSyncedAtUtc);
        }

        public BillingEntitlementSyncResult Sync(BillingEntitlementSyncRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.AccountId))
            {
                return new BillingEntitlementSyncResult
                {
                    Success = false,
                    InSync = false,
                    Message = "AccountId is required."
                };
            }

            lock (_syncRoot)
            {
                string accountId = request.AccountId.Trim();
                var resolved = _pricingEngineService.ResolvePackage(request.Selection);
                string checksum = BuildChecksum(request.Selection, resolved);
                var existing = _records.FindById(accountId);

                if (existing != null && string.Equals(existing.SyncChecksum, checksum, StringComparison.Ordinal))
                {
                    existing.LastSyncedAtUtc = DateTime.UtcNow;
                    UpsertWithRetry(existing);
                    return new BillingEntitlementSyncResult
                    {
                        Success = true,
                        InSync = true,
                        NoChangesDetected = true,
                        Message = "No billing or entitlement changes detected.",
                        Record = existing
                    };
                }

                var record = new BillingEntitlementSyncRecord
                {
                    AccountId = accountId,
                    Selection = request.Selection,
                    ResolvedPackage = resolved,
                    SyncChecksum = checksum,
                    LastSyncedAtUtc = DateTime.UtcNow
                };

                UpsertWithRetry(record);

                return new BillingEntitlementSyncResult
                {
                    Success = true,
                    InSync = true,
                    NoChangesDetected = false,
                    Message = "Billing and entitlements synced successfully.",
                    Record = record
                };
            }
        }

        public BillingEntitlementSyncRecord? GetLatest(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return null;
            }

            lock (_syncRoot)
            {
                return _records.FindById(accountId.Trim());
            }
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private void UpsertWithRetry(BillingEntitlementSyncRecord record)
        {
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _records.Upsert(record);
                    return;
                }
                catch when (attempt < maxAttempts)
                {
                    Thread.Sleep(25 * attempt);
                }
            }
        }

        private static string BuildChecksum(SubscriptionSelection selection, ResolvedPricingPackage resolved)
        {
            return string.Join("|",
                selection.Plan,
                selection.BillingInterval,
                selection.Seats,
                selection.ApplyPlatformFee,
                resolved.Total.ToString(CultureInfo.InvariantCulture),
                resolved.Entitlements.GuardrailsLevel,
                resolved.Entitlements.ConfidentialityModes,
                resolved.Entitlements.AdvancedGuardrails,
                resolved.Entitlements.SharedGlossary,
                resolved.Entitlements.AuditExport,
                resolved.Entitlements.Analytics,
                resolved.Entitlements.TeamAnalytics,
                resolved.Entitlements.SlaTier);
        }
    }
}
