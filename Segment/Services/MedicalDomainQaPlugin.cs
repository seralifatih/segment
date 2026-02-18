using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class MedicalDomainQaPlugin : IDomainQaPlugin
    {
        public const string Id = "medical-dosage-unit";
        private static readonly Regex DosageRegex = new(@"\b\d+(?:[.,]\d+)?\s?(?:mg|mcg|ug|g|kg|ml|mL|l|L|iu|units?|mmol)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string PluginId => Id;

        public IReadOnlyList<GuardrailResult> Evaluate(string sourceText, string translatedText, TranslationContext context)
        {
            var srcDosages = ExtractDosages(sourceText);
            var trgDosages = ExtractDosages(translatedText);

            if (srcDosages.SetEquals(trgDosages))
            {
                return new List<GuardrailResult>();
            }

            return new List<GuardrailResult>
            {
                new()
                {
                    Severity = GuardrailSeverity.Error,
                    RuleId = "MED_DOSAGE_UNIT_MISMATCH",
                    Message = "Medical dosage/unit check failed: dosage values or units changed.",
                    SuggestedFix = "Keep dosage numbers and units exactly aligned with source text.",
                    IsBlocking = true
                }
            };
        }

        private static HashSet<string> ExtractDosages(string text)
        {
            return DosageRegex.Matches(text ?? string.Empty)
                .Select(x => NormalizeWhitespace(x.Value.Trim()))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeWhitespace(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"\s+", string.Empty);
        }
    }
}
