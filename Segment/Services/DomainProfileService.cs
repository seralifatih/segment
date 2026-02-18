using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class DomainProfileService : IDomainProfileService
    {
        private readonly IReadOnlyDictionary<DomainVertical, DomainProfile> _profiles;
        private readonly IReadOnlyDictionary<DomainVertical, DomainRulePack> _rulePacks;

        public DomainProfileService()
        {
            _profiles = BuildProfiles();
            _rulePacks = BuildRulePacks();
        }

        public IReadOnlyList<DomainProfile> GetProfiles()
        {
            return _profiles.Values
                .OrderBy(x => x.Id)
                .ToList();
        }

        public DomainProfile GetProfile(DomainVertical domain)
        {
            return _profiles.TryGetValue(domain, out var profile)
                ? profile
                : throw new InvalidOperationException($"Domain profile not found for {domain}.");
        }

        public DomainRulePack GetRulePack(DomainVertical domain)
        {
            return _rulePacks.TryGetValue(domain, out var pack)
                ? pack
                : throw new InvalidOperationException($"Domain rule pack not found for {domain}.");
        }

        private static IReadOnlyDictionary<DomainVertical, DomainProfile> BuildProfiles()
        {
            return new Dictionary<DomainVertical, DomainProfile>
            {
                [DomainVertical.Legal] = new()
                {
                    Id = DomainVertical.Legal,
                    Name = "Legal",
                    Description = "Contracts, litigation documents, and legal correspondence.",
                    RiskLevel = DomainRiskLevel.Critical,
                    DefaultChecks = new List<string> { "terminology", "numeric_consistency", "date_consistency", "defined_terms" },
                    DefaultStyleHints = new List<string> { "formal_register", "jurisdiction_neutral", "clause_integrity" },
                    RecommendedProviderPolicy = "Local-first for confidential matters; cloud only with approved compliance controls."
                },
                [DomainVertical.Patent] = new()
                {
                    Id = DomainVertical.Patent,
                    Name = "Patent",
                    Description = "Patent claims, abstracts, and prosecution communication.",
                    RiskLevel = DomainRiskLevel.Critical,
                    DefaultChecks = new List<string> { "terminology", "numeric_consistency", "date_consistency", "claim_structure" },
                    DefaultStyleHints = new List<string> { "technical_precision", "no_claim_scope_drift" },
                    RecommendedProviderPolicy = "Local-only for unpublished filings; controlled cloud for public prior-art content."
                },
                [DomainVertical.Medical] = new()
                {
                    Id = DomainVertical.Medical,
                    Name = "Medical",
                    Description = "Clinical reports, patient-facing content, and medical labeling.",
                    RiskLevel = DomainRiskLevel.Critical,
                    DefaultChecks = new List<string> { "terminology", "numeric_consistency", "date_consistency", "unit_consistency" },
                    DefaultStyleHints = new List<string> { "safety_clarity", "regulatory_tone" },
                    RecommendedProviderPolicy = "Approved medical-safe routing with strict PHI handling and local redaction."
                },
                [DomainVertical.Financial] = new()
                {
                    Id = DomainVertical.Financial,
                    Name = "Financial",
                    Description = "Statements, disclosures, and compliance reporting content.",
                    RiskLevel = DomainRiskLevel.High,
                    DefaultChecks = new List<string> { "terminology", "numeric_consistency", "date_consistency", "currency_consistency" },
                    DefaultStyleHints = new List<string> { "audit_ready_style", "concise_disclosure_language" },
                    RecommendedProviderPolicy = "Cloud allowed for non-sensitive reporting; local-only for regulated account data."
                },
                [DomainVertical.GameLocalization] = new()
                {
                    Id = DomainVertical.GameLocalization,
                    Name = "Game Localization",
                    Description = "Narrative, UI strings, and live-ops game content.",
                    RiskLevel = DomainRiskLevel.Medium,
                    DefaultChecks = new List<string> { "terminology", "numeric_consistency", "placeholder_integrity" },
                    DefaultStyleHints = new List<string> { "tone_consistency", "character_voice" },
                    RecommendedProviderPolicy = "Hybrid routing with QA checks on placeholders and lore terms."
                },
                [DomainVertical.Subtitling] = new()
                {
                    Id = DomainVertical.Subtitling,
                    Name = "Subtitling",
                    Description = "Timed subtitle tracks for media localization.",
                    RiskLevel = DomainRiskLevel.Medium,
                    DefaultChecks = new List<string> { "terminology", "numeric_consistency", "timecode_safety" },
                    DefaultStyleHints = new List<string> { "brevity", "readability", "line_break_discipline" },
                    RecommendedProviderPolicy = "Latency-optimized routing with subtitle-safe post-checks."
                },
                [DomainVertical.Ecommerce] = new()
                {
                    Id = DomainVertical.Ecommerce,
                    Name = "E-commerce",
                    Description = "Product listings, catalogs, and conversion copy.",
                    RiskLevel = DomainRiskLevel.Low,
                    DefaultChecks = new List<string> { "terminology", "numeric_consistency", "attribute_integrity" },
                    DefaultStyleHints = new List<string> { "conversion_clarity", "brand_voice" },
                    RecommendedProviderPolicy = "Cloud-first with catalog integrity checks."
                },
                [DomainVertical.CustomerSupport] = new()
                {
                    Id = DomainVertical.CustomerSupport,
                    Name = "Customer Support",
                    Description = "Help center, agent replies, and troubleshooting flows.",
                    RiskLevel = DomainRiskLevel.Low,
                    DefaultChecks = new List<string> { "terminology", "numeric_consistency", "instruction_accuracy" },
                    DefaultStyleHints = new List<string> { "empathy", "clarity", "actionable_steps" },
                    RecommendedProviderPolicy = "Balanced routing with response-time prioritization."
                }
            };
        }

        private static IReadOnlyDictionary<DomainVertical, DomainRulePack> BuildRulePacks()
        {
            return new Dictionary<DomainVertical, DomainRulePack>
            {
                [DomainVertical.Legal] = new()
                {
                    RequireTerminologyChecks = true,
                    RequireNumericChecks = true,
                    RequireDateChecks = true,
                    DisallowedPhrases = new List<string> { "for informational purposes only", "not legal advice", "approximately" },
                    WarningSeverity = DomainRuleSeverity.High,
                    ErrorSeverity = DomainRuleSeverity.Critical
                },
                [DomainVertical.Patent] = new()
                {
                    RequireTerminologyChecks = true,
                    RequireNumericChecks = true,
                    RequireDateChecks = true,
                    DisallowedPhrases = new List<string> { "best effort", "broadly interpreted", "close enough" },
                    WarningSeverity = DomainRuleSeverity.High,
                    ErrorSeverity = DomainRuleSeverity.Critical
                },
                [DomainVertical.Medical] = new()
                {
                    RequireTerminologyChecks = true,
                    RequireNumericChecks = true,
                    RequireDateChecks = true,
                    DisallowedPhrases = new List<string> { "guaranteed cure", "risk-free", "doctor approved*" },
                    WarningSeverity = DomainRuleSeverity.High,
                    ErrorSeverity = DomainRuleSeverity.Critical
                },
                [DomainVertical.Financial] = new()
                {
                    RequireTerminologyChecks = true,
                    RequireNumericChecks = true,
                    RequireDateChecks = true,
                    DisallowedPhrases = new List<string> { "guaranteed return", "risk free", "past performance ensures future results" },
                    WarningSeverity = DomainRuleSeverity.Medium,
                    ErrorSeverity = DomainRuleSeverity.High
                },
                [DomainVertical.GameLocalization] = new()
                {
                    RequireTerminologyChecks = true,
                    RequireNumericChecks = true,
                    RequireDateChecks = false,
                    DisallowedPhrases = new List<string> { "placeholder_text", "TODO", "lorem ipsum" },
                    WarningSeverity = DomainRuleSeverity.Medium,
                    ErrorSeverity = DomainRuleSeverity.High
                },
                [DomainVertical.Subtitling] = new()
                {
                    RequireTerminologyChecks = true,
                    RequireNumericChecks = true,
                    RequireDateChecks = false,
                    DisallowedPhrases = new List<string> { "[inaudible]", "[music]" },
                    WarningSeverity = DomainRuleSeverity.Medium,
                    ErrorSeverity = DomainRuleSeverity.High
                },
                [DomainVertical.Ecommerce] = new()
                {
                    RequireTerminologyChecks = true,
                    RequireNumericChecks = true,
                    RequireDateChecks = false,
                    DisallowedPhrases = new List<string> { "free forever", "lifetime guarantee*" },
                    WarningSeverity = DomainRuleSeverity.Low,
                    ErrorSeverity = DomainRuleSeverity.Medium
                },
                [DomainVertical.CustomerSupport] = new()
                {
                    RequireTerminologyChecks = true,
                    RequireNumericChecks = true,
                    RequireDateChecks = false,
                    DisallowedPhrases = new List<string> { "cannot help", "your issue is not our problem" },
                    WarningSeverity = DomainRuleSeverity.Low,
                    ErrorSeverity = DomainRuleSeverity.Medium
                }
            };
        }
    }
}
