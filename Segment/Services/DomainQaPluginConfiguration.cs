using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class DomainQaPluginConfiguration
    {
        private readonly IReadOnlyDictionary<DomainVertical, IReadOnlyList<string>> _pluginsByDomain;

        public DomainQaPluginConfiguration()
        {
            _pluginsByDomain = new Dictionary<DomainVertical, IReadOnlyList<string>>
            {
                [DomainVertical.Legal] = new List<string> { LegalDomainQaPlugin.Id },
                [DomainVertical.Patent] = new List<string>(),
                [DomainVertical.Medical] = new List<string> { MedicalDomainQaPlugin.Id },
                [DomainVertical.Financial] = new List<string> { FinancialDomainQaPlugin.Id },
                [DomainVertical.GameLocalization] = new List<string>(),
                [DomainVertical.Subtitling] = new List<string> { SubtitlingDomainQaPlugin.Id },
                [DomainVertical.Ecommerce] = new List<string>(),
                [DomainVertical.CustomerSupport] = new List<string>()
            };
        }

        public IReadOnlyList<string> GetEnabledPluginIds(DomainVertical domain)
        {
            return _pluginsByDomain.TryGetValue(domain, out var pluginIds)
                ? pluginIds
                : new List<string>();
        }
    }
}
