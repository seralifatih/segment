using System;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class ObservabilityDashboardService : IObservabilityDashboardService
    {
        private readonly IOnboardingMetricsService _onboardingMetricsService;
        private readonly IReferralService _referralService;
        private readonly IPmfDashboardService _pmfDashboardService;

        public ObservabilityDashboardService(
            IOnboardingMetricsService onboardingMetricsService,
            IReferralService referralService,
            IPmfDashboardService pmfDashboardService)
        {
            _onboardingMetricsService = onboardingMetricsService;
            _referralService = referralService;
            _pmfDashboardService = pmfDashboardService;
        }

        public GrowthObservabilityDashboard BuildDashboard(int pilotWeekWindow = 12)
        {
            var onboardingRecords = _onboardingMetricsService.GetRecords();
            int totalSignups = onboardingRecords.Count;
            int accepted = onboardingRecords.Count(x => x.Outcome == OnboardingOutcome.Accepted);
            int waitlist = onboardingRecords.Count(x => x.Outcome == OnboardingOutcome.Waitlist);
            int rejected = onboardingRecords.Count(x => x.Outcome == OnboardingOutcome.Rejected);

            var referral = _referralService.GetReferralConversionDashboard();
            var snapshots = _pmfDashboardService.GetWeeklySnapshots(Math.Max(1, pilotWeekWindow));
            var latestSnapshot = snapshots.LastOrDefault() ?? new PmfDashboardSnapshot();

            double avgPilotToPaid = snapshots.Count == 0 ? 0 : snapshots.Average(x => x.PilotToPaidConversion);
            double avgChurn = snapshots.Count == 0 ? 0 : snapshots.Average(x => x.ChurnRate);

            return new GrowthObservabilityDashboard
            {
                FunnelDropOff = new FunnelDropOffDashboard
                {
                    TotalSignups = totalSignups,
                    AcceptedCount = accepted,
                    WaitlistCount = waitlist,
                    RejectedCount = rejected,
                    AcceptanceRate = totalSignups == 0 ? 0 : (double)accepted / totalSignups,
                    WaitlistRate = totalSignups == 0 ? 0 : (double)waitlist / totalSignups,
                    RejectionRate = totalSignups == 0 ? 0 : (double)rejected / totalSignups,
                    DropOffRate = totalSignups == 0 ? 0 : (double)rejected / totalSignups
                },
                ReferralConversion = referral,
                PilotConversion = new PilotConversionFunnelDashboard
                {
                    WeeklyPilotActiveUsers = latestSnapshot.Wau,
                    LatestPilotToPaidConversion = latestSnapshot.PilotToPaidConversion,
                    TwelveWeekAveragePilotToPaidConversion = avgPilotToPaid,
                    LatestChurnRate = latestSnapshot.ChurnRate,
                    TwelveWeekAverageChurnRate = avgChurn
                }
            };
        }
    }
}
