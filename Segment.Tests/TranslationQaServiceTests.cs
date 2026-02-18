using System.Collections.Generic;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class TranslationQaServiceTests
    {
        private readonly TranslationQaService _service = new();

        [Fact]
        public void Evaluate_Should_Flag_Glossary_Adherence_Issue_When_Locked_Target_Missing()
        {
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                LockedTerminology = new Dictionary<string, string>
                {
                    ["governing law"] = "uygulanacak hukuk"
                }
            };

            var result = _service.Evaluate(
                "The governing law is Turkish law.",
                "Bu sozlesme Turk hukukuna tabidir.",
                context);

            result.Results.Should().Contain(x => x.RuleId == "QA_GLOSSARY_ADHERENCE" && x.SeverityScore == 70 && !x.IsBlocking);
        }

        [Fact]
        public void Evaluate_Should_Flag_Number_And_Date_Consistency_Issues()
        {
            var result = _service.Evaluate(
                "Payment of 1200 is due on 2026-03-01.",
                "Odeme 1100 tutarinda ve 2026-04-01 tarihinde yapilacaktir.",
                new TranslationContext());

            result.Results.Should().Contain(x => x.RuleId == "QA_NUMBER_CONSISTENCY");
            result.Results.Should().Contain(x => x.RuleId == "QA_DATE_CONSISTENCY");
        }

        [Fact]
        public void Evaluate_Should_Flag_Punctuation_And_Tag_Parity_Issues()
        {
            var result = _service.Evaluate(
                "Click <b>Save</b> now.",
                "Simdi kaydetin!",
                new TranslationContext());

            result.Results.Should().Contain(x => x.RuleId == "QA_PUNCTUATION_PARITY");
            result.Results.Should().Contain(x => x.RuleId == "QA_TAG_PARITY" && x.IsBlocking);
        }

        [Fact]
        public void Evaluate_Should_Promote_Warnings_To_Blocking_In_Strict_Legal_Mode()
        {
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                StrictQaMode = true,
                LockedTerminology = new Dictionary<string, string>
                {
                    ["indemnification"] = "tazminat"
                }
            };

            var result = _service.Evaluate(
                "The indemnification amount is 1200.",
                "Sorumluluk tutari 1100.",
                context);

            result.Results.Should().Contain(x => x.RuleId == "QA_GLOSSARY_ADHERENCE" && x.IsBlocking);
            result.Results.Should().Contain(x => x.RuleId == "QA_NUMBER_CONSISTENCY" && x.IsBlocking);
        }

        [Fact]
        public void Evaluate_Should_Keep_Warnings_NonBlocking_When_Strict_Mode_Is_Off()
        {
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                StrictQaMode = false,
                LockedTerminology = new Dictionary<string, string>
                {
                    ["indemnification"] = "tazminat"
                }
            };

            var result = _service.Evaluate(
                "The indemnification amount is 1200.",
                "Sorumluluk tutari 1100.",
                context);

            result.Results.Should().Contain(x => x.RuleId == "QA_GLOSSARY_ADHERENCE" && !x.IsBlocking);
            result.Results.Should().Contain(x => x.RuleId == "QA_NUMBER_CONSISTENCY" && !x.IsBlocking);
        }
    }
}
