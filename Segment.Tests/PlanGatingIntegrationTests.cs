using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class PlanGatingIntegrationTests
    {
        [Fact]
        public void GatedFeatureDenialPath_Should_Block_AuditExport_On_FreelancerPlan()
        {
            string previousPlan = SettingsService.Current.ActivePricingPlan;
            try
            {
                SettingsService.Current.ActivePricingPlan = PricingPlan.LegalProIndividual.ToString();

                var pricing = new PricingEngineService();
                var gating = new PlanEntitlementService(pricing);
                PlanEntitlements entitlements = gating.ResolveActiveEntitlements();
                EntitlementCheckResult check = gating.CheckFeature(EntitlementFeature.AuditExport);

                entitlements.AuditExport.Should().BeFalse();
                check.Allowed.Should().BeFalse();
                check.Message.Should().Contain("Current package: Freelancer Pro");
            }
            finally
            {
                SettingsService.Current.ActivePricingPlan = previousPlan;
            }
        }
    }
}
