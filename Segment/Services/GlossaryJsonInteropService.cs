using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class GlossaryJsonInteropService
    {
        public void ExportProfile(string filePath, string profileName, bool isGlobal)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Export path is required.", nameof(filePath));

            string safeProfile = string.IsNullOrWhiteSpace(profileName)
                ? (isGlobal ? "Global" : GlossaryService.CurrentProfile.Name)
                : profileName.Trim();

            if (!isGlobal)
            {
                GlossaryService.GetOrCreateProfile(safeProfile);
            }

            var profile = isGlobal ? GlossaryService.GlobalProfile : GlossaryService.CurrentProfile;
            var terms = profile.Terms.FindAll()
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Source))
                .ToDictionary(x => x.Source, x => x, StringComparer.OrdinalIgnoreCase);

            var payload = new LegacyGlossaryPayload
            {
                Name = profile.Name,
                IsFrozen = profile.IsFrozen,
                Terms = terms
            };

            string fullPath = Path.GetFullPath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }

        public int ImportProfile(string filePath, bool isGlobal)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Import path is required.", nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Glossary JSON not found.", filePath);

            LegacyGlossaryPayload? payload = JsonSerializer.Deserialize<LegacyGlossaryPayload>(File.ReadAllText(filePath));
            if (payload == null)
            {
                return 0;
            }

            if (!isGlobal)
            {
                string profileName = string.IsNullOrWhiteSpace(payload.Name) ? "ImportedProfile" : payload.Name.Trim();
                GlossaryService.GetOrCreateProfile(profileName);
                GlossaryService.CurrentProfile.IsFrozen = payload.IsFrozen;
                GlossaryService.SaveProfile(GlossaryService.CurrentProfile);
            }

            return GlossaryService.AddTerms(payload.Terms?.Values ?? Enumerable.Empty<TermEntry>(), isGlobal);
        }

        private sealed class LegacyGlossaryPayload
        {
            public string Name { get; set; } = "Default";
            public Dictionary<string, TermEntry> Terms { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public bool IsFrozen { get; set; }
        }
    }
}
