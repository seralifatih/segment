using System;
using Segment.App.Models;
using System.Threading;

namespace Segment.App.Services
{
    public class OnboardingFunnelService : IOnboardingFunnelService
    {
        private readonly IGtmConfigService _gtmConfigService;
        private readonly IOnboardingQualificationService _qualificationService;
        private readonly IOnboardingMetricsService _metricsService;

        public OnboardingFunnelService(
            IGtmConfigService gtmConfigService,
            IOnboardingQualificationService qualificationService,
            IOnboardingMetricsService metricsService)
        {
            _gtmConfigService = gtmConfigService;
            _qualificationService = qualificationService;
            _metricsService = metricsService;
        }

        public OnboardingDecision Process(OnboardingProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var phase = _gtmConfigService.GetActiveLaunchPhase();
            string correlationId = Guid.NewGuid().ToString("N");
            var decision = _qualificationService.Evaluate(profile, phase);

            var metric = new OnboardingMetricRecord
            {
                CorrelationId = correlationId,
                CreatedAtUtc = DateTime.UtcNow,
                LaunchPhase = phase,
                Role = profile.Role,
                DomainFocus = profile.DomainFocus?.Trim() ?? string.Empty,
                DomainIncludesLegal = (profile.DomainFocus ?? string.Empty).Contains("legal", StringComparison.OrdinalIgnoreCase),
                WeeklyLegalVolumeEstimate = Math.Max(0, profile.WeeklyLegalVolumeEstimate),
                ConfidentialityRequirementLevel = profile.ConfidentialityRequirementLevel,
                IntendsGlossaryUsage = profile.IntendsGlossaryUsage,
                EligibilityScore = decision.Score,
                Outcome = decision.Outcome
            };

            RecordMetricWithRetry(metric);

            return decision;
        }

        private void RecordMetricWithRetry(OnboardingMetricRecord metric)
        {
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _metricsService.Record(metric);
                    return;
                }
                catch when (attempt < maxAttempts)
                {
                    Thread.Sleep(20 * attempt);
                }
            }
        }
    }
}
