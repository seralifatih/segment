using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class InteroperabilityService
    {
        private readonly InteroperabilityConnectorRegistry _registry;
        private readonly GlossaryJsonInteropService _glossaryJsonInteropService;
        private readonly INicheTemplateService _nicheTemplateService;

        public InteroperabilityService(
            InteroperabilityConnectorRegistry? registry = null,
            GlossaryJsonInteropService? glossaryJsonInteropService = null,
            INicheTemplateService? nicheTemplateService = null)
        {
            _registry = registry ?? new InteroperabilityConnectorRegistry();
            _glossaryJsonInteropService = glossaryJsonInteropService ?? new GlossaryJsonInteropService();
            _nicheTemplateService = nicheTemplateService ?? new NicheTemplateService();
        }

        public int ImportTerms(string format, string filePath, bool isGlobal, InteropTermTransferOptions? options = null)
        {
            if (string.Equals(format, "glossary-json", StringComparison.OrdinalIgnoreCase))
            {
                return _glossaryJsonInteropService.ImportProfile(filePath, isGlobal);
            }

            var connector = _registry.ResolveForImport(format);
            IReadOnlyList<TermEntry> imported = connector.ImportTerms(format, filePath, options ?? new InteropTermTransferOptions());
            return GlossaryService.AddTerms(imported, isGlobal);
        }

        public void ExportTerms(string format, string filePath, bool isGlobal, string profileName, InteropTermTransferOptions? options = null)
        {
            if (string.Equals(format, "glossary-json", StringComparison.OrdinalIgnoreCase))
            {
                _glossaryJsonInteropService.ExportProfile(filePath, profileName, isGlobal);
                return;
            }

            var connector = _registry.ResolveForExport(format);
            string safeProfile = string.IsNullOrWhiteSpace(profileName) ? GlossaryService.CurrentProfile.Name : profileName.Trim();
            if (!isGlobal)
            {
                GlossaryService.GetOrCreateProfile(safeProfile);
            }

            var profile = isGlobal ? GlossaryService.GlobalProfile : GlossaryService.CurrentProfile;
            IReadOnlyList<TermEntry> terms = profile.Terms.FindAll()
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Source) && !string.IsNullOrWhiteSpace(x.Target))
                .ToList();
            connector.ExportTerms(format, filePath, terms, options ?? new InteropTermTransferOptions());
        }

        public void ApplyExternalProjectMapping(string projectProfileName, ExternalProjectProfileMapping mapping)
        {
            if (string.IsNullOrWhiteSpace(projectProfileName))
            {
                throw new ArgumentException("Project profile name is required.", nameof(projectProfileName));
            }

            if (!_nicheTemplateService.TryGetProjectConfiguration(projectProfileName, out ProjectNicheConfiguration configuration))
            {
                configuration = new ProjectNicheConfiguration
                {
                    ProjectProfileName = projectProfileName.Trim(),
                    Domain = Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical parsed) ? parsed : DomainVertical.Legal,
                    StyleHints = new DomainProfileService().GetProfile(Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical d) ? d : DomainVertical.Legal).DefaultStyleHints,
                    EnabledQaChecks = new DomainQaPluginConfiguration().GetEnabledPluginIds(Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical q) ? q : DomainVertical.Legal).ToList()
                };
            }

            configuration.ExternalMapping = mapping ?? new ExternalProjectProfileMapping();
            _nicheTemplateService.SaveProjectConfiguration(configuration);
        }
    }
}
