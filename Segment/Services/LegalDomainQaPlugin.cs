using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class LegalDomainQaPlugin : IDomainQaPlugin
    {
        public const string Id = "legal-core";

        private static readonly Regex NumberRegex = new(@"\b\d+(?:[.,]\d+)?\b", RegexOptions.Compiled);
        private static readonly Regex DateRegex = new(@"\b(?:\d{1,2}[./-]\d{1,2}[./-]\d{2,4}|\d{4}[./-]\d{1,2}[./-]\d{1,2})\b", RegexOptions.Compiled);
        private static readonly Regex AllCapsEntityRegex = new(@"\b[A-Z]{2,}(?:\s+[A-Z]{2,})*\b", RegexOptions.Compiled);
        private static readonly Regex ProperEntityRegex = new(@"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+){1,3}\b", RegexOptions.Compiled);

        private static readonly string[] ModalShallTokens = { "shall", "zorundadir", "zorundadir", "edecektir", "etmelidir", "gerekmektedir" };
        private static readonly string[] ModalMustTokens = { "must", "have to", "zorunda", "zorunlu", "gerekir", "mecbur" };
        private static readonly string[] ModalMayTokens = { "may", "can", "yapabilir", "olabilir", "hakki vardir", "hakki vardir" };

        public string PluginId => Id;

        public IReadOnlyList<GuardrailResult> Evaluate(string sourceText, string translatedText, TranslationContext context)
        {
            string source = sourceText ?? string.Empty;
            string translated = translatedText ?? string.Empty;
            var safeContext = context ?? new TranslationContext();

            var results = new List<GuardrailResult>();
            results.AddRange(CheckLockedTerminology(source, translated, safeContext.LockedTerminology));
            results.AddRange(CheckNumericAndDateConsistency(source, translated));
            results.AddRange(CheckEntityConsistency(source, translated));
            results.AddRange(CheckModalVerbSensitivity(source, translated));
            return results;
        }

        private static IEnumerable<GuardrailResult> CheckLockedTerminology(
            string source,
            string translated,
            IReadOnlyDictionary<string, string> terminology)
        {
            if (terminology == null || terminology.Count == 0)
            {
                yield break;
            }

            foreach (var kvp in terminology)
            {
                string sourceTerm = kvp.Key?.Trim() ?? string.Empty;
                string targetTerm = kvp.Value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sourceTerm) || string.IsNullOrWhiteSpace(targetTerm))
                {
                    continue;
                }

                bool sourceContainsTerm = ContainsToken(source, sourceTerm);
                bool translatedContainsTarget = ContainsToken(translated, targetTerm);
                if (sourceContainsTerm && !translatedContainsTarget)
                {
                    yield return new GuardrailResult
                    {
                        Severity = GuardrailSeverity.Error,
                        RuleId = "LEGAL_LOCKED_TERMINOLOGY",
                        Message = $"Locked term '{sourceTerm}' must map to '{targetTerm}'.",
                        SuggestedFix = $"Use '{targetTerm}' for '{sourceTerm}' in translated text.",
                        IsBlocking = true
                    };
                }
            }
        }

        private static IEnumerable<GuardrailResult> CheckNumericAndDateConsistency(string source, string translated)
        {
            var srcNumbers = ExtractMatches(NumberRegex, source);
            var trgNumbers = ExtractMatches(NumberRegex, translated);
            if (!srcNumbers.SetEquals(trgNumbers))
            {
                yield return new GuardrailResult
                {
                    Severity = GuardrailSeverity.Error,
                    RuleId = "LEGAL_NUMERIC_MISMATCH",
                    Message = "Numeric mismatch detected between source and translation.",
                    SuggestedFix = "Align all numbers and decimal values with source text.",
                    IsBlocking = true
                };
            }

            var srcDates = ExtractMatches(DateRegex, source);
            var trgDates = ExtractMatches(DateRegex, translated);
            if (!srcDates.SetEquals(trgDates))
            {
                yield return new GuardrailResult
                {
                    Severity = GuardrailSeverity.Error,
                    RuleId = "LEGAL_DATE_MISMATCH",
                    Message = "Date mismatch detected between source and translation.",
                    SuggestedFix = "Ensure all date values match source text exactly.",
                    IsBlocking = true
                };
            }
        }

        private static IEnumerable<GuardrailResult> CheckEntityConsistency(string source, string translated)
        {
            var entities = ExtractEntityCandidates(source);
            foreach (string entity in entities)
            {
                if (!ContainsToken(translated, entity))
                {
                    yield return new GuardrailResult
                    {
                        Severity = GuardrailSeverity.Error,
                        RuleId = "LEGAL_ENTITY_MISMATCH",
                        Message = $"Potential party/entity mismatch for '{entity}'.",
                        SuggestedFix = $"Preserve party/entity naming for '{entity}' from source text.",
                        IsBlocking = true
                    };
                }
            }
        }

        private static IEnumerable<GuardrailResult> CheckModalVerbSensitivity(string source, string translated)
        {
            bool sourceShall = ContainsAny(source, ModalShallTokens);
            bool sourceMust = ContainsAny(source, ModalMustTokens);
            bool sourceMay = ContainsAny(source, ModalMayTokens);

            bool targetShall = ContainsAny(translated, ModalShallTokens);
            bool targetMust = ContainsAny(translated, ModalMustTokens);
            bool targetMay = ContainsAny(translated, ModalMayTokens);

            if (sourceShall && !targetShall)
            {
                yield return new GuardrailResult
                {
                    Severity = GuardrailSeverity.Error,
                    RuleId = "LEGAL_MODAL_SHALL_SENSITIVITY",
                    Message = "Mandatory modality drift: 'shall' equivalent was not preserved.",
                    SuggestedFix = "Use obligation-equivalent wording for 'shall'.",
                    IsBlocking = true
                };
            }

            if (sourceMust && !targetMust)
            {
                yield return new GuardrailResult
                {
                    Severity = GuardrailSeverity.Error,
                    RuleId = "LEGAL_MODAL_MUST_SENSITIVITY",
                    Message = "Mandatory modality drift: 'must' equivalent was not preserved.",
                    SuggestedFix = "Use strict obligation-equivalent wording for 'must'.",
                    IsBlocking = true
                };
            }

            if (sourceMay && !targetMay)
            {
                yield return new GuardrailResult
                {
                    Severity = GuardrailSeverity.Warning,
                    RuleId = "LEGAL_MODAL_MAY_SENSITIVITY",
                    Message = "Permissive modality drift: 'may' equivalent was not preserved.",
                    SuggestedFix = "Use permissive-equivalent wording for 'may'.",
                    IsBlocking = true
                };
            }
        }

        private static HashSet<string> ExtractMatches(Regex regex, string text)
        {
            return regex.Matches(text ?? string.Empty)
                .Select(x => x.Value.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> ExtractEntityCandidates(string source)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in AllCapsEntityRegex.Matches(source ?? string.Empty))
            {
                if (match.Value.Length >= 3)
                {
                    set.Add(match.Value.Trim());
                }
            }

            foreach (Match match in ProperEntityRegex.Matches(source ?? string.Empty))
            {
                string entity = match.Value.Trim();
                if (!entity.Equals("This Agreement", StringComparison.OrdinalIgnoreCase)
                    && !entity.Equals("The Party", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(entity);
                }
            }

            return set;
        }

        private static bool ContainsAny(string text, IEnumerable<string> tokens)
        {
            string candidate = text ?? string.Empty;
            return tokens.Any(token => ContainsToken(candidate, token));
        }

        private static bool ContainsToken(string text, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            string pattern = $@"\b{Regex.Escape(token.Trim())}\b";
            return Regex.IsMatch(text ?? string.Empty, pattern, RegexOptions.IgnoreCase);
        }
    }
}
