using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class PlanEntitlementServiceTests
    {
        private readonly PlanEntitlementService _service = new(new PricingEngineService());

        [Fact]
        public void CheckFeature_Should_Deny_AuditExport_For_FreelancerPro()
        {
            string previousPlan = SettingsService.Current.ActivePricingPlan;
            try
            {
                SettingsService.Current.ActivePricingPlan = PricingPlan.LegalProIndividual.ToString();
                EntitlementCheckResult result = _service.CheckFeature(EntitlementFeature.AuditExport);

                result.Allowed.Should().BeFalse();
                result.Message.Should().Contain("Upgrade to Agency Team or Enterprise");
            }
            finally
            {
                SettingsService.Current.ActivePricingPlan = previousPlan;
            }
        }

        [Fact]
        public void CheckFeature_Should_Allow_SharedGlossary_For_AgencyTeam()
        {
            string previousPlan = SettingsService.Current.ActivePricingPlan;
            try
            {
                SettingsService.Current.ActivePricingPlan = PricingPlan.LegalTeam.ToString();
                EntitlementCheckResult result = _service.CheckFeature(EntitlementFeature.SharedGlossaryWorkspace);

                result.Allowed.Should().BeTrue();
            }
            finally
            {
                SettingsService.Current.ActivePricingPlan = previousPlan;
            }
        }

        [Fact]
        public void ConfidentialityMode_Should_Be_Restricted_By_Plan()
        {
            string previousPlan = SettingsService.Current.ActivePricingPlan;
            try
            {
                SettingsService.Current.ActivePricingPlan = PricingPlan.LegalProIndividual.ToString();
                _service.IsConfidentialityModeAllowed("AirGapReview").Should().BeFalse();

                SettingsService.Current.ActivePricingPlan = PricingPlan.EnterpriseLegalAssurance.ToString();
                _service.IsConfidentialityModeAllowed("AirGapReview").Should().BeTrue();
            }
            finally
            {
                SettingsService.Current.ActivePricingPlan = previousPlan;
            }
        }
    }
}
