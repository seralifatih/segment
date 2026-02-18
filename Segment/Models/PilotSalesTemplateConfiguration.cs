namespace Segment.App.Models
{
    public class PilotSalesTemplateConfiguration
    {
        public double FreelancerHourlyRateUsd { get; set; } = 75;
        public int EstimatedMonthlyTaskVolume { get; set; } = 250;

        public string SecurityPrivacyOnePagerTemplate { get; set; } =
            """
            Security & Privacy One-Pager
            Pilot: {{PilotName}}
            Agency: {{AgencyName}}

            Data handling:
            - Customer content is processed for translation quality improvement only.
            - Access is role-scoped and limited to pilot users.
            - Logs are retained according to pilot governance controls.

            Operational controls:
            - Median response performance: {{AssistedMinutesPerTask}} min/task (pilot benchmark).
            - Terminology violation rate reduced from {{BaselineViolationRatePct}} to {{AssistedViolationRatePct}}.
            - Confidence signal: {{ConfidenceSummary}}
            """;

        public string PilotSuccessCriteriaTemplate { get; set; } =
            """
            Pilot Success Criteria Sheet
            Session: {{SessionId}}

            Primary targets:
            - Time savings >= 15% (current: {{TimeSavedPct}})
            - Violation reduction >= 20% (current: {{ViolationReductionPct}})
            - Acceptance lift >= 5pp (current: {{AcceptanceDeltaPct}})

            Sample health:
            - Baseline samples: {{BaselineSamples}}
            - Assisted samples: {{AssistedSamples}}
            - Confidence assessment: {{ConfidenceSummary}}
            """;

        public string PricingProposalSummaryTemplate { get; set; } =
            """
            Pricing Proposal Summary
            Agency: {{AgencyName}}
            Segment: Paid pilot conversion

            Value baseline:
            - Estimated monthly tasks: {{EstimatedMonthlyTaskVolume}}
            - Estimated monthly hours saved: {{EstimatedMonthlyHoursSaved}}
            - Estimated monthly value: {{EstimatedMonthlyCostSavingsUsd}}
            - Estimated annual value: {{EstimatedAnnualCostSavingsUsd}}

            Suggested commercial framing:
            - Preserve quality gains ({{ViolationReductionPct}} violation reduction)
            - Capture speed gains ({{TimeSavedPct}} time saved)
            - Align pricing to measurable delivered value
            """;
    }
}
