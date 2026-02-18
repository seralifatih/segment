using Segment.App.Models;

namespace Segment.App.Services
{
    public class LaunchPhaseGateService : ILaunchPhaseGateService
    {
        public const string FeatureOnboarding = "onboarding";
        public const string FeatureSelfServeSignup = "self_serve_signup";
        public const string FeatureAgencyAccess = "agency_access";
        public const string FeatureLegalNicheAccess = "legal_niche_access";

        private readonly IGtmConfigService _gtmConfigService;

        public LaunchPhaseGateService(IGtmConfigService gtmConfigService)
        {
            _gtmConfigService = gtmConfigService;
        }

        public bool IsFeatureEnabled(string feature, LaunchUserContext userContext)
        {
            var phase = _gtmConfigService.GetActiveLaunchPhase();
            return IsFeatureEnabledForPhase(feature, userContext, phase);
        }

        public bool CanInviteUser(LaunchUserContext userContext)
        {
            var phase = _gtmConfigService.GetActiveLaunchPhase();

            return phase switch
            {
                LaunchPhase.PrivateBeta =>
                    userContext.IsAdminUser &&
                    userContext.IsInvitedUser &&
                    userContext.IsLegalNicheUser,

                LaunchPhase.PaidPilot =>
                    userContext.IsAdminUser &&
                    userContext.HasPilotContract &&
                    (userContext.IsLegalNicheUser || userContext.IsAgencyAccount),

                LaunchPhase.Scale =>
                    userContext.IsAdminUser,

                _ =>
                    userContext.IsAdminUser && userContext.IsLegalNicheUser
            };
        }

        public string BuildPhasePreview(LaunchPhase phase, LaunchUserContext userContext)
        {
            bool onboarding = IsFeatureEnabledForPhase(FeatureOnboarding, userContext, phase);
            bool selfServe = IsFeatureEnabledForPhase(FeatureSelfServeSignup, userContext, phase);
            bool agencyAccess = IsFeatureEnabledForPhase(FeatureAgencyAccess, userContext, phase);
            bool invite = CanInviteUserForPhase(userContext, phase);

            return $"Onboarding: {(onboarding ? "Allowed" : "Blocked")} | " +
                   $"Self-Serve: {(selfServe ? "Enabled" : "Disabled")} | " +
                   $"Agency: {(agencyAccess ? "Enabled" : "Disabled")} | " +
                   $"Invites: {(invite ? "Allowed" : "Restricted")}";
        }

        private static bool IsFeatureEnabledForPhase(string feature, LaunchUserContext userContext, LaunchPhase phase)
        {
            return feature switch
            {
                FeatureOnboarding => phase switch
                {
                    LaunchPhase.PrivateBeta =>
                        userContext.IsInvitedUser && userContext.IsLegalNicheUser,
                    LaunchPhase.PaidPilot =>
                        (userContext.IsLegalNicheUser || userContext.IsAgencyAccount) && userContext.HasPilotContract,
                    LaunchPhase.Scale =>
                        true,
                    _ =>
                        userContext.IsLegalNicheUser
                },

                FeatureSelfServeSignup => phase == LaunchPhase.Scale,

                FeatureAgencyAccess => phase is LaunchPhase.PaidPilot or LaunchPhase.Scale,

                FeatureLegalNicheAccess => phase != LaunchPhase.Scale || userContext.IsLegalNicheUser,

                _ => false
            };
        }

        private static bool CanInviteUserForPhase(LaunchUserContext userContext, LaunchPhase phase)
        {
            return phase switch
            {
                LaunchPhase.PrivateBeta =>
                    userContext.IsAdminUser &&
                    userContext.IsInvitedUser &&
                    userContext.IsLegalNicheUser,

                LaunchPhase.PaidPilot =>
                    userContext.IsAdminUser &&
                    userContext.HasPilotContract &&
                    (userContext.IsLegalNicheUser || userContext.IsAgencyAccount),

                LaunchPhase.Scale =>
                    userContext.IsAdminUser,

                _ =>
                    userContext.IsAdminUser && userContext.IsLegalNicheUser
            };
        }
    }
}
