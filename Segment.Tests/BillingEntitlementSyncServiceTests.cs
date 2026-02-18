using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class BillingEntitlementSyncServiceTests
    {
        [Fact]
        public void Sync_Should_Be_Idempotent_When_No_Billing_Changes()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "SegmentBillingSyncTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);

            try
            {
                using var service = new BillingEntitlementSyncService(basePath: basePath);
                var request = new BillingEntitlementSyncRequest
                {
                    AccountId = "acct-1",
                    Selection = new SubscriptionSelection
                    {
                        Plan = PricingPlan.LegalTeam,
                        BillingInterval = BillingInterval.Monthly,
                        Seats = 5,
                        ApplyPlatformFee = true
                    }
                };

                BillingEntitlementSyncResult first = service.Sync(request);
                BillingEntitlementSyncResult second = service.Sync(request);

                first.Success.Should().BeTrue();
                first.NoChangesDetected.Should().BeFalse();
                second.Success.Should().BeTrue();
                second.NoChangesDetected.Should().BeTrue();
                second.InSync.Should().BeTrue();
                second.Record.Should().NotBeNull();
            }
            finally
            {
                try
                {
                    if (Directory.Exists(basePath))
                    {
                        Directory.Delete(basePath, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }
    }
}
