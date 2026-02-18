using System.Collections.Generic;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class DomainQaPluginTests
    {
        [Fact]
        public void LegalPlugin_Should_Flag_Modal_Drift()
        {
            var plugin = new LegalDomainQaPlugin();
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                LockedTerminology = new Dictionary<string, string>()
            };

            var results = plugin.Evaluate(
                "The supplier shall keep records.",
                "Tedarikci kayit tutabilir.",
                context);

            results.Should().Contain(x => x.RuleId == "LEGAL_MODAL_SHALL_SENSITIVITY" && x.IsBlocking);
        }

        [Fact]
        public void FinancialPlugin_Should_Flag_Numeric_Fidelity_Mismatch()
        {
            var plugin = new FinancialDomainQaPlugin();
            var context = new TranslationContext { Domain = DomainVertical.Financial };

            var results = plugin.Evaluate(
                "Revenue increased from 1200 to 1450.",
                "Gelir 1200'den 1400'e cikti.",
                context);

            results.Should().ContainSingle(x => x.RuleId == "FIN_NUMERIC_FIDELITY" && x.IsBlocking);
        }

        [Fact]
        public void MedicalPlugin_Should_Flag_Dosage_Unit_Mismatch()
        {
            var plugin = new MedicalDomainQaPlugin();
            var context = new TranslationContext { Domain = DomainVertical.Medical };

            var results = plugin.Evaluate(
                "Administer 5 mg twice daily.",
                "Gunde iki kez 10 mg uygulayin.",
                context);

            results.Should().ContainSingle(x => x.RuleId == "MED_DOSAGE_UNIT_MISMATCH" && x.IsBlocking);
        }

        [Fact]
        public void SubtitlingPlugin_Should_Flag_Cpl_Violation()
        {
            var plugin = new SubtitlingDomainQaPlugin();
            var context = new TranslationContext { Domain = DomainVertical.Subtitling };

            var results = plugin.Evaluate(
                "Hello there.",
                "This subtitle line is intentionally made very long to exceed readability cpl limits.",
                context);

            results.Should().ContainSingle(x => x.RuleId == "SUB_CPL_LIMIT" && x.Severity == GuardrailSeverity.Warning);
        }
    }
}
