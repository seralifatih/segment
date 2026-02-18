using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class PilotSalesArtifactExportService : IPilotSalesArtifactExportService
    {
        private readonly IRoiBenchmarkCalculator _roiBenchmarkCalculator;
        private readonly PilotSalesTemplateRenderer _templateRenderer;

        public PilotSalesArtifactExportService(
            IRoiBenchmarkCalculator? roiBenchmarkCalculator = null,
            PilotSalesTemplateRenderer? templateRenderer = null)
        {
            _roiBenchmarkCalculator = roiBenchmarkCalculator ?? new RoiBenchmarkCalculator();
            _templateRenderer = templateRenderer ?? new PilotSalesTemplateRenderer();
        }

        public PilotSalesExportPackage ExportPackage(
            BenchmarkSession session,
            string outputDirectory,
            PilotSalesTemplateConfiguration? templateConfiguration = null,
            string? packageName = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

            templateConfiguration ??= new PilotSalesTemplateConfiguration();

            string safePackageName = SanitizeFileName(string.IsNullOrWhiteSpace(packageName) ? $"pilot-sales-{session.Id}" : packageName!);
            string packageFolderName = $"{safePackageName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            string packageDirectory = Path.Combine(outputDirectory, packageFolderName);
            Directory.CreateDirectory(packageDirectory);

            PilotRoiReport report = _roiBenchmarkCalculator.Calculate(session);
            var bindings = BuildBindings(session, report, templateConfiguration);
            DateTime generatedAtUtc = DateTime.UtcNow;

            var artifactTemplates = new (string Key, string Title, string Template)[]
            {
                ("roi_scorecard", "ROI Scorecard", BuildRoiScorecardTemplate()),
                ("freelancer_value_summary", "Value Summary - Freelancer", BuildFreelancerValueTemplate()),
                ("agency_procurement_value_summary", "Value Summary - Agency Procurement", BuildAgencyProcurementTemplate()),
                ("security_privacy_one_pager", "Security & Privacy One-Pager", templateConfiguration.SecurityPrivacyOnePagerTemplate),
                ("pilot_success_criteria_sheet", "Pilot Success Criteria Sheet", templateConfiguration.PilotSuccessCriteriaTemplate),
                ("pricing_proposal_summary", "Pricing Proposal Summary", templateConfiguration.PricingProposalSummaryTemplate)
            };

            var artifacts = new List<PilotSalesArtifactDocument>();
            foreach ((string key, string title, string template) in artifactTemplates)
            {
                string rendered = _templateRenderer.Render(template, bindings);
                string baseFileName = SanitizeFileName(key);
                string textPath = Path.Combine(packageDirectory, $"{baseFileName}.txt");
                string pdfPath = Path.Combine(packageDirectory, $"{baseFileName}.pdf");

                File.WriteAllText(textPath, rendered);
                SimplePdfWriter.WriteSinglePage(pdfPath, title, rendered);

                artifacts.Add(new PilotSalesArtifactDocument
                {
                    Key = key,
                    Title = title,
                    TextPath = textPath,
                    PdfPath = pdfPath,
                    GeneratedAtUtc = generatedAtUtc
                });
            }

            string zipPath = Path.Combine(outputDirectory, $"{packageFolderName}.zip");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(packageDirectory, zipPath);

            return new PilotSalesExportPackage
            {
                SessionId = session.Id,
                PackageDirectory = packageDirectory,
                ZipPath = zipPath,
                GeneratedAtUtc = generatedAtUtc,
                Artifacts = artifacts
            };
        }

        private static Dictionary<string, string> BuildBindings(
            BenchmarkSession session,
            PilotRoiReport report,
            PilotSalesTemplateConfiguration config)
        {
            double minutesSavedPerTask = Math.Max(0, report.BaselineAverageMinutesPerTask - report.AssistedAverageMinutesPerTask);
            double monthlyMinutesSaved = minutesSavedPerTask * Math.Max(0, config.EstimatedMonthlyTaskVolume);
            double monthlyHoursSaved = monthlyMinutesSaved / 60.0;
            double monthlyCostSavings = monthlyHoursSaved * Math.Max(0, config.FreelancerHourlyRateUsd);
            double annualCostSavings = monthlyCostSavings * 12.0;

            string agencyName = string.IsNullOrWhiteSpace(session.PilotName) ? "Pilot Agency" : session.PilotName.Trim();

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SessionId"] = report.SessionId,
                ["PilotName"] = string.IsNullOrWhiteSpace(session.PilotName) ? report.SessionId : session.PilotName,
                ["AgencyName"] = agencyName,
                ["GeneratedAtUtc"] = report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ["BaselineMinutesPerTask"] = report.BaselineAverageMinutesPerTask.ToString("F2", CultureInfo.InvariantCulture),
                ["AssistedMinutesPerTask"] = report.AssistedAverageMinutesPerTask.ToString("F2", CultureInfo.InvariantCulture),
                ["TimeSavedPct"] = $"{report.TimeSavedPercentage:F2}%",
                ["BaselineViolationRatePct"] = $"{report.BaselineViolationRate:P2}",
                ["AssistedViolationRatePct"] = $"{report.AssistedViolationRate:P2}",
                ["ViolationReductionPct"] = $"{report.ViolationReductionPercentage:F2}%",
                ["BaselineAcceptanceRatePct"] = $"{report.BaselineAcceptanceRate:P2}",
                ["AssistedAcceptanceRatePct"] = $"{report.AssistedAcceptanceRate:P2}",
                ["AcceptanceDeltaPct"] = $"{report.AcceptanceRateDelta * 100.0:F2}pp",
                ["BaselineEditRatePct"] = $"{report.BaselineEditRate:P2}",
                ["AssistedEditRatePct"] = $"{report.AssistedEditRate:P2}",
                ["EditDeltaPct"] = $"{report.EditRateDelta * 100.0:F2}pp",
                ["BaselineSamples"] = report.TotalBaselineSamples.ToString(CultureInfo.InvariantCulture),
                ["AssistedSamples"] = report.TotalAssistedSamples.ToString(CultureInfo.InvariantCulture),
                ["ConfidenceSummary"] = report.ConfidenceSummary,
                ["EstimatedMonthlyTaskVolume"] = config.EstimatedMonthlyTaskVolume.ToString(CultureInfo.InvariantCulture),
                ["EstimatedMonthlyHoursSaved"] = monthlyHoursSaved.ToString("F1", CultureInfo.InvariantCulture),
                ["EstimatedMonthlyCostSavingsUsd"] = monthlyCostSavings.ToString("C0", CultureInfo.GetCultureInfo("en-US")),
                ["EstimatedAnnualCostSavingsUsd"] = annualCostSavings.ToString("C0", CultureInfo.GetCultureInfo("en-US"))
            };
        }

        private static string BuildRoiScorecardTemplate()
        {
            return """
                   ROI Scorecard
                   Session: {{SessionId}}
                   Generated: {{GeneratedAtUtc}} UTC

                   Efficiency:
                   - Baseline minutes/task: {{BaselineMinutesPerTask}}
                   - Assisted minutes/task: {{AssistedMinutesPerTask}}
                   - Time saved: {{TimeSavedPct}}

                   Quality:
                   - Baseline violation rate: {{BaselineViolationRatePct}}
                   - Assisted violation rate: {{AssistedViolationRatePct}}
                   - Violation reduction: {{ViolationReductionPct}}

                   Adoption:
                   - Baseline acceptance rate: {{BaselineAcceptanceRatePct}}
                   - Assisted acceptance rate: {{AssistedAcceptanceRatePct}}
                   - Acceptance delta: {{AcceptanceDeltaPct}}

                   Sample confidence:
                   - Baseline samples: {{BaselineSamples}}
                   - Assisted samples: {{AssistedSamples}}
                   - {{ConfidenceSummary}}
                   """;
        }

        private static string BuildFreelancerValueTemplate()
        {
            return """
                   Value Summary for Freelancer
                   Pilot: {{PilotName}}

                   Practical outcomes:
                   - {{TimeSavedPct}} faster translation cycle per task.
                   - {{ViolationReductionPct}} fewer terminology violations.
                   - {{AcceptanceDeltaPct}} acceptance lift with lower rework trend.

                   Commercial implication:
                   - Estimated monthly hours saved: {{EstimatedMonthlyHoursSaved}}
                   - Estimated monthly value captured: {{EstimatedMonthlyCostSavingsUsd}}
                   - Estimated annual value captured: {{EstimatedAnnualCostSavingsUsd}}
                   """;
        }

        private static string BuildAgencyProcurementTemplate()
        {
            return """
                   Value Summary for Agency Procurement
                   Agency: {{AgencyName}}

                   Business case highlights:
                   - Efficiency uplift: {{TimeSavedPct}}
                   - Quality risk reduction: {{ViolationReductionPct}}
                   - Throughput-ready sample base: {{AssistedSamples}} assisted tasks

                   Procurement framing:
                   - Quantified monthly value opportunity: {{EstimatedMonthlyCostSavingsUsd}}
                   - Quantified annual value opportunity: {{EstimatedAnnualCostSavingsUsd}}
                   - Confidence statement: {{ConfidenceSummary}}
                   """;
        }

        private static string SanitizeFileName(string value)
        {
            string candidate = string.IsNullOrWhiteSpace(value) ? "pilot-sales-package" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                candidate = candidate.Replace(c, '_');
            }

            return candidate;
        }
    }
}
