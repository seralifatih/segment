using System;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class PlanEntitlementService : IPlanEntitlementService
    {
        private readonly IPricingEngineService _pricingEngineService;

        public PlanEntitlementService(IPricingEngineService? pricingEngineService = null)
        {
            _pricingEngineService = pricingEngineService ?? new PricingEngineService();
        }

        public PlanEntitlements ResolveActiveEntitlements()
        {
            PricingPlan plan = ResolveActivePlan();
            return _pricingEngineService.ResolveEntitlements(plan);
        }

        public EntitlementCheckResult CheckFeature(EntitlementFeature feature)
        {
            PricingPlan plan = ResolveActivePlan();
            PlanEntitlements entitlements = _pricingEngineService.ResolveEntitlements(plan);
            string package = GetPackageLabel(plan);

            return feature switch
            {
                EntitlementFeature.AdvancedGuardrails => Check(
                    IsAdvancedGuardrails(entitlements),
                    package,
                    "Advanced guardrails are available on Agency Team and Enterprise."),
                EntitlementFeature.SharedGlossaryWorkspace => Check(
                    entitlements.SharedGlossary,
                    package,
                    "Shared glossary workspace is available on Agency Team and Enterprise."),
                EntitlementFeature.AuditExport => Check(
                    entitlements.AuditExport,
                    package,
                    "Audit export is available on Agency Team and Enterprise."),
                EntitlementFeature.ConfidentialityModes => Check(
                    IsAdvancedConfidentiality(entitlements),
                    package,
                    "Advanced confidentiality modes are available on Agency Team and Enterprise."),
                EntitlementFeature.TeamAnalytics => Check(
                    IsTeamAnalytics(entitlements),
                    package,
                    "Team analytics is available on Agency Team and Enterprise."),
                _ => new EntitlementCheckResult { Allowed = false, Message = $"Feature is not available on {package}." }
            };
        }

        public bool IsConfidentialityModeAllowed(string mode)
        {
            string safeMode = string.IsNullOrWhiteSpace(mode) ? "Standard" : mode.Trim();
            PlanEntitlements entitlements = ResolveActiveEntitlements();
            if (safeMode.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string normalized = entitlements.ConfidentialityModes ?? string.Empty;
            return normalized.Contains(safeMode, StringComparison.OrdinalIgnoreCase);
        }

        public string GetActivePackageLabel()
        {
            return GetPackageLabel(ResolveActivePlan());
        }

        public string BuildEntitlementSummary()
        {
            PlanEntitlements entitlements = ResolveActiveEntitlements();
            return
                $"Package: {GetActivePackageLabel()}\n" +
                $"Advanced Guardrails: {(IsAdvancedGuardrails(entitlements) ? "Enabled" : "Locked")}\n" +
                $"Shared Glossary Workspace: {(entitlements.SharedGlossary ? "Enabled" : "Locked")}\n" +
                $"Audit Export: {(entitlements.AuditExport ? "Enabled" : "Locked")}\n" +
                $"Confidentiality Modes: {entitlements.ConfidentialityModes}\n" +
                $"Team Analytics: {(IsTeamAnalytics(entitlements) ? "Enabled" : "Locked")}";
        }

        private static EntitlementCheckResult Check(bool allowed, string packageLabel, string lockedMessage)
        {
            return new EntitlementCheckResult
            {
                Allowed = allowed,
                Message = allowed
                    ? $"Feature available on {packageLabel}."
                    : $"{lockedMessage} Current package: {packageLabel}. Upgrade to Agency Team or Enterprise."
            };
        }

        private static bool IsAdvancedGuardrails(PlanEntitlements entitlements)
        {
            return (entitlements.GuardrailsLevel ?? string.Empty).Contains("Advanced", StringComparison.OrdinalIgnoreCase)
                || (entitlements.GuardrailsLevel ?? string.Empty).Contains("AssuranceMax", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAdvancedConfidentiality(PlanEntitlements entitlements)
        {
            string modes = entitlements.ConfidentialityModes ?? string.Empty;
            return modes.Contains("TeamConfidential", StringComparison.OrdinalIgnoreCase)
                || modes.Contains("AirGapReview", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTeamAnalytics(PlanEntitlements entitlements)
        {
            return entitlements.Analytics && entitlements.SlaTier != SlaTier.Standard;
        }

        private static PricingPlan ResolveActivePlan()
        {
            if (!Enum.TryParse(SettingsService.Current.ActivePricingPlan, out PricingPlan plan))
            {
                return PricingPlan.LegalProIndividual;
            }

            return plan;
        }

        private static string GetPackageLabel(PricingPlan plan)
        {
            return plan switch
            {
                PricingPlan.LegalProIndividual => "Freelancer Pro",
                PricingPlan.LegalTeam => "Agency Team",
                PricingPlan.EnterpriseLegalAssurance => "Enterprise",
                _ => "Freelancer Pro"
            };
        }
    }
}
