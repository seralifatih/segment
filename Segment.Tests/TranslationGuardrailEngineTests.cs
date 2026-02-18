using System.Collections.Generic;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class TranslationGuardrailEngineTests
    {
        private readonly TranslationGuardrailEngine _engine = new();

        [Fact]
        public void Validate_Should_Flag_Locked_Terminology_Violation_For_Legal()
        {
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                LockedTerminology = new Dictionary<string, string>
                {
                    ["indemnification"] = "tazminat"
                }
            };

            var result = _engine.Validate(
                "The indemnification clause applies.",
                "Maddede sorumluluk duzenlenir.",
                context);

            result.Results.Should().Contain(x => x.RuleId == "LEGAL_LOCKED_TERMINOLOGY" && x.IsBlocking);
        }

        [Fact]
        public void Validate_Should_Flag_Numeric_Or_Date_Mismatch_For_Legal()
        {
            var context = new TranslationContext { Domain = DomainVertical.Legal };
            var result = _engine.Validate(
                "Payment of 1200 is due on 2026-03-01.",
                "Odeme 1100 tutarinda ve 2026-04-01 tarihinde yapilacaktir.",
                context);

            result.Results.Should().Contain(x => x.RuleId == "LEGAL_NUMERIC_MISMATCH");
            result.Results.Should().Contain(x => x.RuleId == "LEGAL_DATE_MISMATCH");
        }

        [Fact]
        public void Validate_Should_Flag_Entity_Mismatch_For_Legal()
        {
            var context = new TranslationContext { Domain = DomainVertical.Legal };
            var result = _engine.Validate(
                "ACME CORP agrees with Beta LLC.",
                "Taraflar mutabik kalmistir.",
                context);

            result.Results.Should().Contain(x => x.RuleId == "LEGAL_ENTITY_MISMATCH" && x.IsBlocking);
        }

        [Fact]
        public void Validate_Should_Flag_Modal_Verb_Sensitivity_Drift_For_Legal()
        {
            var context = new TranslationContext { Domain = DomainVertical.Legal };
            var result = _engine.Validate(
                "The supplier shall deliver the records.",
                "Tedarikci kayitlari teslim edebilir.",
                context);

            result.Results.Should().Contain(x => x.RuleId == "LEGAL_MODAL_SHALL_SENSITIVITY" && x.IsBlocking);
        }

        [Fact]
        public void Validate_Should_Return_No_Blocking_Issues_When_Legal_Text_Is_Aligned()
        {
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                LockedTerminology = new Dictionary<string, string>
                {
                    ["indemnification"] = "tazminat"
                }
            };

            var result = _engine.Validate(
                "ACME CORP shall pay 1200 on 2026-03-01 under indemnification terms.",
                "ACME CORP tazminat sartlari kapsaminda 2026-03-01 tarihinde 1200 odeme yapmak zorundadir.",
                context);

            result.HasBlockingIssues.Should().BeFalse();
        }
    }
}
