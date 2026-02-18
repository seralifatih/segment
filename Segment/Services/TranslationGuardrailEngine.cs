using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class TranslationGuardrailEngine : ITranslationGuardrailEngine
    {
        private readonly IReadOnlyDictionary<string, IDomainQaPlugin> _plugins;
        private readonly DomainQaPluginConfiguration _pluginConfiguration;

        public TranslationGuardrailEngine()
            : this(
                new IDomainQaPlugin[]
                {
                    new LegalDomainQaPlugin(),
                    new FinancialDomainQaPlugin(),
                    new MedicalDomainQaPlugin(),
                    new SubtitlingDomainQaPlugin()
                },
                new DomainQaPluginConfiguration())
        {
        }

        internal TranslationGuardrailEngine(IEnumerable<IDomainQaPlugin> plugins, DomainQaPluginConfiguration pluginConfiguration)
        {
            _plugins = (plugins ?? Enumerable.Empty<IDomainQaPlugin>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PluginId))
                .GroupBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            _pluginConfiguration = pluginConfiguration ?? new DomainQaPluginConfiguration();
        }

        public GuardrailValidationResult Validate(string sourceText, string translatedText, TranslationContext context)
        {
            string source = sourceText ?? string.Empty;
            string translated = translatedText ?? string.Empty;
            var safeContext = context ?? new TranslationContext();

            var results = new List<GuardrailResult>();
            var enabledPluginIds = (safeContext.EnabledQaChecks != null && safeContext.EnabledQaChecks.Count > 0
                    ? safeContext.EnabledQaChecks
                    : _pluginConfiguration.GetEnabledPluginIds(safeContext.Domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string pluginId in enabledPluginIds)
            {
                if (!_plugins.TryGetValue(pluginId, out var plugin))
                {
                    continue;
                }

                var pluginResults = plugin.Evaluate(source, translated, safeContext);
                if (pluginResults == null || pluginResults.Count == 0)
                {
                    continue;
                }

                results.AddRange(pluginResults.Where(x => x != null));
            }

            return new GuardrailValidationResult { Results = results };
        }
    }
}
