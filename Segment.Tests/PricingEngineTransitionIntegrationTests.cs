using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class PricingEngineTransitionIntegrationTests
    {
        private readonly PricingEngineService _engine = new();

        [Fact]
        public void Upgrade_Individual_To_Team_Should_Apply_Seat_Minimum_And_Team_Entitlements()
        {
            var current = new SubscriptionSelection
            {
                Plan = PricingPlan.LegalProIndividual,
                BillingInterval = BillingInterval.Monthly,
                Seats = 1,
                ApplyPlatformFee = true
            };

            var transition = _engine.Upgrade(current, PricingPlan.LegalTeam, requestedSeats: 2);
            transition.Allowed.Should().BeTrue();
            transition.UpdatedSelection.Seats.Should().Be(3);

            var resolved = _engine.ResolvePackage(transition.UpdatedSelection);
            resolved.EffectiveSeats.Should().Be(3);
            resolved.Entitlements.SharedGlossary.Should().BeTrue();
            resolved.PlatformFee.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Downgrade_Enterprise_To_Individual_Should_Reset_To_Single_Seat()
        {
            var current = new SubscriptionSelection
            {
                Plan = PricingPlan.EnterpriseLegalAssurance,
                BillingInterval = BillingInterval.Annual,
                Seats = 25,
                ApplyPlatformFee = true
            };

            var transition = _engine.Downgrade(current, PricingPlan.LegalProIndividual, requestedSeats: 12);
            transition.Allowed.Should().BeTrue();
            transition.UpdatedSelection.Seats.Should().Be(1);
            transition.UpdatedSelection.Plan.Should().Be(PricingPlan.LegalProIndividual);

            var resolved = _engine.ResolvePackage(transition.UpdatedSelection);
            resolved.Entitlements.SharedGlossary.Should().BeFalse();
            resolved.PlatformFee.Should().Be(0);
        }
    }
}
