using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class FinancialDomainQaPlugin : IDomainQaPlugin
    {
        public const string Id = "financial-numeric-fidelity";
        private static readonly Regex NumberRegex = new(@"\b\d+(?:[.,]\d+)?\b", RegexOptions.Compiled);

        public string PluginId => Id;

        public IReadOnlyList<GuardrailResult> Evaluate(string sourceText, string translatedText, TranslationContext context)
        {
            var srcNumbers = ExtractNumbers(sourceText);
            var trgNumbers = ExtractNumbers(translatedText);

            if (srcNumbers.SetEquals(trgNumbers))
            {
                return new List<GuardrailResult>();
            }

            return new List<GuardrailResult>
            {
                new()
                {
                    Severity = GuardrailSeverity.Error,
                    RuleId = "FIN_NUMERIC_FIDELITY",
                    Message = "Financial numeric fidelity check failed: numbers diverge between source and translation.",
                    SuggestedFix = "Align amount, percentage, and ratio values with source text.",
                    IsBlocking = true
                }
            };
        }

        private static HashSet<string> ExtractNumbers(string text)
        {
            return NumberRegex.Matches(text ?? string.Empty)
                .Select(x => x.Value.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
