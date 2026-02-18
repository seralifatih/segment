using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Segment.App.Services
{
    public class PilotSalesTemplateRenderer
    {
        private static readonly Regex PlaceholderPattern = new(@"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);

        public string Render(string template, IReadOnlyDictionary<string, string> bindings)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));

            return PlaceholderPattern.Replace(template, m =>
            {
                string key = m.Groups["key"].Value;
                return bindings.TryGetValue(key, out string? value) ? value ?? string.Empty : string.Empty;
            });
        }
    }
}
