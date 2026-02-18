using System;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class OnboardingQualificationService : IOnboardingQualificationService
    {
        public OnboardingDecision Evaluate(OnboardingProfile profile, LaunchPhase phase)
        {
            bool domainIncludesLegal = IncludesLegal(profile.DomainFocus);
            int minimumLegalUsageThreshold = GetMinimumLegalUsageThreshold(phase);

            if (phase is LaunchPhase.PrivateBeta or LaunchPhase.PaidPilot && !domainIncludesLegal)
            {
                return new OnboardingDecision
                {
                    Outcome = OnboardingOutcome.Rejected,
                    Score = 0,
                    Explanation = "Early access is currently limited to legal-focused use cases."
                };
            }

            if (profile.WeeklyLegalVolumeEstimate < minimumLegalUsageThreshold)
            {
                return new OnboardingDecision
                {
                    Outcome = OnboardingOutcome.Rejected,
                    Score = 0,
                    Explanation = $"Minimum weekly legal workload is {minimumLegalUsageThreshold} items in this phase."
                };
            }

            int score = 0;

            score += domainIncludesLegal ? 35 : 5;
            score += ScoreByVolume(profile.WeeklyLegalVolumeEstimate);
            score += profile.IntendsGlossaryUsage ? 20 : 0;
            score += profile.Role switch
            {
                OnboardingRole.Freelancer => 8,
                OnboardingRole.Agency => 14,
                OnboardingRole.Enterprise => 18,
                _ => 0
            };
            score += profile.ConfidentialityRequirementLevel switch
            {
                ConfidentialityRequirementLevel.Standard => 2,
                ConfidentialityRequirementLevel.High => 8,
                ConfidentialityRequirementLevel.Strict => 12,
                _ => 0
            };

            (int accept, int waitlist) thresholds = GetThresholds(phase);
            OnboardingOutcome outcome = score >= thresholds.accept
                ? OnboardingOutcome.Accepted
                : score >= thresholds.waitlist
                    ? OnboardingOutcome.Waitlist
                    : OnboardingOutcome.Rejected;

            return new OnboardingDecision
            {
                Outcome = outcome,
                Score = score,
                Explanation = outcome switch
                {
                    OnboardingOutcome.Accepted => phase == LaunchPhase.PaidPilot
                        ? "Accepted into Paid Pilot."
                        : "Accepted into active launch cohort.",
                    OnboardingOutcome.Waitlist => "Added to waitlist due to current cohort capacity and fit prioritization.",
                    _ => "Current profile fit is below launch-phase qualification thresholds."
                }
            };
        }

        private static (int accept, int waitlist) GetThresholds(LaunchPhase phase)
        {
            return phase switch
            {
                LaunchPhase.PrivateBeta => (70, 50),
                LaunchPhase.PaidPilot => (75, 60),
                LaunchPhase.Scale => (60, 45),
                _ => (65, 50)
            };
        }

        private static int GetMinimumLegalUsageThreshold(LaunchPhase phase)
        {
            return phase switch
            {
                LaunchPhase.PrivateBeta => 20,
                LaunchPhase.PaidPilot => 30,
                LaunchPhase.Scale => 5,
                _ => 15
            };
        }

        private static int ScoreByVolume(int weeklyVolume)
        {
            return weeklyVolume switch
            {
                >= 200 => 35,
                >= 100 => 28,
                >= 50 => 18,
                >= 30 => 12,
                >= 20 => 8,
                >= 10 => 4,
                _ => 0
            };
        }

        private static bool IncludesLegal(string domainFocus)
        {
            return !string.IsNullOrWhiteSpace(domainFocus)
                && domainFocus.Contains("legal", StringComparison.OrdinalIgnoreCase);
        }
    }
}
