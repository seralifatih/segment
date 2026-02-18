using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class ProviderRoutingPolicyTests
    {
        [Fact]
        public void ResolveRoutingDecision_Should_Block_Cloud_When_LocalOnly_Mode()
        {
            var settings = new AppConfig
            {
                AiProvider = "Google",
                ConfidentialityMode = "LocalOnly",
                ConfidentialProjectLocalOnly = false
            };

            ProviderRoutingDecision decision = TranslationService.ResolveRoutingDecision(settings);

            decision.IsBlocked.Should().BeTrue();
            decision.ConfidentialityMode.Should().Be(ConfidentialityMode.LocalOnly);
            decision.EffectiveRoute.Should().Contain("Blocked");
        }

        [Fact]
        public void ResolveRoutingDecision_Should_Redact_Before_Cloud_In_RedactedCloud_Mode()
        {
            var settings = new AppConfig
            {
                AiProvider = "Custom",
                ConfidentialityMode = "RedactedCloud",
                ConfidentialProjectLocalOnly = false
            };

            ProviderRoutingDecision decision = TranslationService.ResolveRoutingDecision(settings);

            decision.IsBlocked.Should().BeFalse();
            decision.ApplyRedactionBeforeCloudCall.Should().BeTrue();
            decision.EffectiveRoute.Should().Be("Custom");
        }

        [Fact]
        public void ResolveRoutingDecision_Should_Use_Normal_Routing_In_Standard_Mode()
        {
            var settings = new AppConfig
            {
                AiProvider = "Google",
                ConfidentialityMode = "Standard",
                ConfidentialProjectLocalOnly = false
            };

            ProviderRoutingDecision decision = TranslationService.ResolveRoutingDecision(settings);

            decision.IsBlocked.Should().BeFalse();
            decision.ApplyRedactionBeforeCloudCall.Should().BeFalse();
            decision.EffectiveRoute.Should().Be("Google");
        }
    }
}
