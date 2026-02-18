using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class SubtitlingDomainQaPlugin : IDomainQaPlugin
    {
        public const string Id = "subtitling-length-cpl";
        private const int MaxCharactersPerLine = 42;

        public string PluginId => Id;

        public IReadOnlyList<GuardrailResult> Evaluate(string sourceText, string translatedText, TranslationContext context)
        {
            var lines = (translatedText ?? string.Empty)
                .Split('\n')
                .Select(x => x.TrimEnd('\r'))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var results = new List<GuardrailResult>();
            for (int i = 0; i < lines.Count; i++)
            {
                int cpl = lines[i].Length;
                if (cpl <= MaxCharactersPerLine)
                {
                    continue;
                }

                results.Add(new GuardrailResult
                {
                    Severity = GuardrailSeverity.Warning,
                    RuleId = "SUB_CPL_LIMIT",
                    Message = $"Subtitle line {i + 1} exceeds CPL limit ({cpl}>{MaxCharactersPerLine}).",
                    SuggestedFix = "Split or shorten subtitle lines to meet readability constraints.",
                    IsBlocking = false
                });
            }

            return results;
        }
    }
}
