using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class TranslationQaService : ITranslationQaService
    {
        private static readonly Regex NumberRegex = new(@"\b\d+(?:[.,]\d+)?\b", RegexOptions.Compiled);
        private static readonly Regex DateRegex = new(@"\b(?:\d{4}-\d{2}-\d{2}|\d{2}[./-]\d{2}[./-]\d{4})\b", RegexOptions.Compiled);
        private static readonly Regex TagRegex = new(@"<\/?([a-zA-Z][a-zA-Z0-9:_-]*)\b[^>]*>", RegexOptions.Compiled);

        public GuardrailValidationResult Evaluate(string sourceText, string translatedText, TranslationContext context)
        {
            string source = sourceText ?? string.Empty;
            string target = translatedText ?? string.Empty;
            var safeContext = context ?? new TranslationContext();

            var issues = new List<GuardrailResult>();
            issues.AddRange(CheckGlossaryAdherence(source, target, safeContext));
            issues.AddRange(CheckNumberDateConsistency(source, target));
            issues.AddRange(CheckPunctuationAndTagParity(source, target));

            bool strict = safeContext.StrictQaMode && IsRegulatoryDomain(safeContext.Domain);
            if (strict)
            {
                foreach (GuardrailResult issue in issues.Where(x => x.Severity >= GuardrailSeverity.Warning))
                {
                    issue.IsBlocking = true;
                }
            }

            return new GuardrailValidationResult { Results = issues };
        }

        private static IEnumerable<GuardrailResult> CheckGlossaryAdherence(string source, string target, TranslationContext context)
        {
            foreach (var kvp in context.LockedTerminology ?? new Dictionary<string, string>())
            {
                string sourceTerm = (kvp.Key ?? string.Empty).Trim();
                string expectedTarget = (kvp.Value ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(sourceTerm) || string.IsNullOrWhiteSpace(expectedTarget))
                {
                    continue;
                }

                if (!ContainsIgnoreCase(source, sourceTerm))
                {
                    continue;
                }

                if (!ContainsIgnoreCase(target, expectedTarget))
                {
                    yield return new GuardrailResult
                    {
                        RuleId = "QA_GLOSSARY_ADHERENCE",
                        Severity = GuardrailSeverity.Warning,
                        SeverityScore = 70,
                        IsBlocking = false,
                        Message = $"Expected glossary target '{expectedTarget}' for source term '{sourceTerm}' was not found.",
                        SuggestedFix = "Use locked terminology target exactly for the detected source term."
                    };
                }
            }
        }

        private static IEnumerable<GuardrailResult> CheckNumberDateConsistency(string source, string target)
        {
            var sourceNumbers = NumberRegex.Matches(source).Select(x => x.Value).ToList();
            var targetNumbers = NumberRegex.Matches(target).Select(x => x.Value).ToList();
            if (!sourceNumbers.SequenceEqual(targetNumbers, StringComparer.Ordinal))
            {
                yield return new GuardrailResult
                {
                    RuleId = "QA_NUMBER_CONSISTENCY",
                    Severity = GuardrailSeverity.Warning,
                    SeverityScore = 65,
                    IsBlocking = false,
                    Message = $"Numeric token mismatch. Source: [{string.Join(", ", sourceNumbers)}], Target: [{string.Join(", ", targetNumbers)}].",
                    SuggestedFix = "Ensure all numeric values are preserved in the same order."
                };
            }

            var sourceDates = DateRegex.Matches(source).Select(x => NormalizeDate(x.Value)).ToList();
            var targetDates = DateRegex.Matches(target).Select(x => NormalizeDate(x.Value)).ToList();
            if (!sourceDates.SequenceEqual(targetDates, StringComparer.Ordinal))
            {
                yield return new GuardrailResult
                {
                    RuleId = "QA_DATE_CONSISTENCY",
                    Severity = GuardrailSeverity.Warning,
                    SeverityScore = 65,
                    IsBlocking = false,
                    Message = $"Date token mismatch. Source: [{string.Join(", ", sourceDates)}], Target: [{string.Join(", ", targetDates)}].",
                    SuggestedFix = "Preserve date values exactly; only localize formatting when approved."
                };
            }
        }

        private static IEnumerable<GuardrailResult> CheckPunctuationAndTagParity(string source, string target)
        {
            string sourceTerminal = GetTerminalPunctuation(source);
            string targetTerminal = GetTerminalPunctuation(target);
            if (!string.Equals(sourceTerminal, targetTerminal, StringComparison.Ordinal))
            {
                yield return new GuardrailResult
                {
                    RuleId = "QA_PUNCTUATION_PARITY",
                    Severity = GuardrailSeverity.Warning,
                    SeverityScore = 55,
                    IsBlocking = false,
                    Message = $"Terminal punctuation mismatch. Source '{sourceTerminal}', target '{targetTerminal}'.",
                    SuggestedFix = "Keep sentence-final punctuation parity unless style guide says otherwise."
                };
            }

            var sourceTags = ExtractTags(source);
            var targetTags = ExtractTags(target);
            if (!sourceTags.SequenceEqual(targetTags, StringComparer.OrdinalIgnoreCase))
            {
                yield return new GuardrailResult
                {
                    RuleId = "QA_TAG_PARITY",
                    Severity = GuardrailSeverity.Error,
                    SeverityScore = 90,
                    IsBlocking = true,
                    Message = "Markup/tag parity mismatch between source and translation.",
                    SuggestedFix = "Preserve opening/closing tags and attributes exactly."
                };
            }
        }

        private static bool ContainsIgnoreCase(string text, string needle)
        {
            return text?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRegulatoryDomain(DomainVertical domain)
        {
            return domain == DomainVertical.Legal || domain == DomainVertical.Medical || domain == DomainVertical.Financial;
        }

        private static string GetTerminalPunctuation(string value)
        {
            string trimmed = (value ?? string.Empty).TrimEnd();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            char last = trimmed[^1];
            if (last == '.' || last == '!' || last == '?' || last == ';' || last == ':')
            {
                return last.ToString();
            }

            return string.Empty;
        }

        private static IReadOnlyList<string> ExtractTags(string value)
        {
            return TagRegex.Matches(value ?? string.Empty)
                .Select(x => x.Value.Trim())
                .ToList();
        }

        private static string NormalizeDate(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return value;
        }
    }
}
