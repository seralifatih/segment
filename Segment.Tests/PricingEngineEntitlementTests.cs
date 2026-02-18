using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class PricingEngineEntitlementTests
    {
        private readonly PricingEngineService _engine = new();

        [Fact]
        public void LegalProIndividual_Should_Resolve_Standard_Entitlements()
        {
            var entitlements = _engine.ResolveEntitlements(PricingPlan.LegalProIndividual);

            entitlements.SharedGlossary.Should().BeFalse();
            entitlements.AuditExport.Should().BeFalse();
            entitlements.Analytics.Should().BeTrue();
            entitlements.AdvancedGuardrails.Should().BeFalse();
            entitlements.TeamAnalytics.Should().BeFalse();
            entitlements.SlaTier.Should().Be(SlaTier.Standard);
        }

        [Fact]
        public void LegalTeam_Should_Resolve_Collaboration_Entitlements()
        {
            var entitlements = _engine.ResolveEntitlements(PricingPlan.LegalTeam);

            entitlements.SharedGlossary.Should().BeTrue();
            entitlements.AuditExport.Should().BeTrue();
            entitlements.Analytics.Should().BeTrue();
            entitlements.AdvancedGuardrails.Should().BeTrue();
            entitlements.TeamAnalytics.Should().BeTrue();
            entitlements.SlaTier.Should().Be(SlaTier.Business);
        }

        [Fact]
        public void EnterpriseLegalAssurance_Should_Resolve_Strict_Entitlements()
        {
            var entitlements = _engine.ResolveEntitlements(PricingPlan.EnterpriseLegalAssurance);

            entitlements.ConfidentialityModes.Should().Contain("AirGapReview");
            entitlements.AdvancedGuardrails.Should().BeTrue();
            entitlements.TeamAnalytics.Should().BeTrue();
            entitlements.SlaTier.Should().Be(SlaTier.Enterprise);
        }
    }
}
