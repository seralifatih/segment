using System.Collections.Generic;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class TranslationPastebackGuardIntegrationTests
    {
        [Fact]
        public void Evaluate_Should_Block_AutoPaste_When_Blocking_Issue_Exists()
        {
            var coordinator = new TranslationPastebackCoordinator(new TranslationGuardrailEngine());
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                LockedTerminology = new Dictionary<string, string>
                {
                    ["governing law"] = "uygulanacak hukuk"
                }
            };

            PastebackDecision decision = coordinator.Evaluate(
                "The governing law shall be Turkish law.",
                "Bu sozlesme Turk hukukuna tabidir.",
                context);

            decision.AutoPasteAllowed.Should().BeFalse();
            decision.BlockingIssues.Should().NotBeEmpty();
        }

        [Fact]
        public void Evaluate_Should_Allow_AutoPaste_When_No_Blocking_Issue_Exists()
        {
            var coordinator = new TranslationPastebackCoordinator(new TranslationGuardrailEngine());
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                LockedTerminology = new Dictionary<string, string>
                {
                    ["governing law"] = "uygulanacak hukuk"
                }
            };

            PastebackDecision decision = coordinator.Evaluate(
                "The governing law shall be Turkish law and fee is 1200 on 2026-03-01.",
                "Uygulanacak hukuk Turk hukukudur ve taraf 1200 ucreti 2026-03-01 tarihinde odemek zorundadir.",
                context);

            decision.AutoPasteAllowed.Should().BeTrue();
            decision.BlockingIssues.Should().BeEmpty();
        }

        [Fact]
        public void Evaluate_Should_Block_When_Strict_Qa_Mode_Promotes_Warnings()
        {
            var coordinator = new TranslationPastebackCoordinator(new TranslationGuardrailEngine(), new TranslationQaService());
            var context = new TranslationContext
            {
                Domain = DomainVertical.Legal,
                StrictQaMode = true,
                LockedTerminology = new Dictionary<string, string>
                {
                    ["indemnification"] = "tazminat"
                }
            };

            PastebackDecision decision = coordinator.Evaluate(
                "The indemnification amount is 1200.",
                "Sorumluluk tutari 1100.",
                context);

            decision.AutoPasteAllowed.Should().BeFalse();
            decision.BlockingIssues.Should().Contain(x => x.RuleId == "QA_GLOSSARY_ADHERENCE");
        }
    }
}
