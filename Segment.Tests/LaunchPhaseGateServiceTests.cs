using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class LaunchPhaseGateServiceTests
    {
        [Fact]
        public void PrivateBeta_Should_Block_Onboarding_Without_Invite()
        {
            using var scope = new GtmScope();
            scope.SetPhase(LaunchPhase.PrivateBeta);
            var gateService = new LaunchPhaseGateService(scope.ConfigService);

            var user = new LaunchUserContext
            {
                IsLegalNicheUser = true,
                IsInvitedUser = false
            };

            gateService.IsFeatureEnabled(LaunchPhaseGateService.FeatureOnboarding, user).Should().BeFalse();
        }

        [Fact]
        public void PaidPilot_Should_Require_Pilot_Contract_And_Allow_Agency()
        {
            using var scope = new GtmScope();
            scope.SetPhase(LaunchPhase.PaidPilot);
            var gateService = new LaunchPhaseGateService(scope.ConfigService);

            var agencyWithoutContract = new LaunchUserContext
            {
                IsAgencyAccount = true,
                HasPilotContract = false
            };
            gateService.IsFeatureEnabled(LaunchPhaseGateService.FeatureOnboarding, agencyWithoutContract).Should().BeFalse();

            var agencyWithContract = new LaunchUserContext
            {
                IsAgencyAccount = true,
                HasPilotContract = true
            };
            gateService.IsFeatureEnabled(LaunchPhaseGateService.FeatureOnboarding, agencyWithContract).Should().BeTrue();
        }

        [Fact]
        public void Scale_Should_Enable_SelfServe_Onboarding()
        {
            using var scope = new GtmScope();
            scope.SetPhase(LaunchPhase.Scale);
            var gateService = new LaunchPhaseGateService(scope.ConfigService);

            var unknownUser = new LaunchUserContext();
            gateService.IsFeatureEnabled(LaunchPhaseGateService.FeatureSelfServeSignup, unknownUser).Should().BeTrue();
            gateService.IsFeatureEnabled(LaunchPhaseGateService.FeatureOnboarding, unknownUser).Should().BeTrue();
        }

        [Fact]
        public void Phase_Transition_Should_Change_Gating_Decision()
        {
            using var scope = new GtmScope();
            var gateService = new LaunchPhaseGateService(scope.ConfigService);

            var user = new LaunchUserContext
            {
                IsInvitedUser = false,
                IsLegalNicheUser = true,
                HasPilotContract = true
            };

            scope.SetPhase(LaunchPhase.PrivateBeta);
            gateService.IsFeatureEnabled(LaunchPhaseGateService.FeatureOnboarding, user).Should().BeFalse();

            scope.SetPhase(LaunchPhase.PaidPilot);
            gateService.IsFeatureEnabled(LaunchPhaseGateService.FeatureOnboarding, user).Should().BeTrue();
        }

        [Fact]
        public void Integration_PrivateBeta_InviteOnly_Should_Block_Onboarding()
        {
            using var scope = new GtmScope();
            scope.SetPhase(LaunchPhase.PrivateBeta);
            var gateService = new LaunchPhaseGateService(scope.ConfigService);

            var onboardingUser = new LaunchUserContext
            {
                IsInvitedUser = false,
                IsLegalNicheUser = true
            };

            bool isAllowed = gateService.IsFeatureEnabled(LaunchPhaseGateService.FeatureOnboarding, onboardingUser);
            isAllowed.Should().BeFalse();
        }

        private sealed class GtmScope : IDisposable
        {
            private readonly string _basePath;
            public GtmConfigService ConfigService { get; }

            public GtmScope()
            {
                _basePath = Path.Combine(Path.GetTempPath(), "SegmentLaunchPhaseTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_basePath);
                ConfigService = new GtmConfigService(_basePath);
                ConfigService.LoadConfig();
            }

            public void SetPhase(LaunchPhase phase)
            {
                var config = ConfigService.LoadConfig();
                config.ActiveLaunchPhase = phase;
                ConfigService.SaveConfig(config);
            }

            public void Dispose()
            {
                ConfigService.Dispose();
                try
                {
                    if (Directory.Exists(_basePath))
                    {
                        Directory.Delete(_basePath, recursive: true);
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
