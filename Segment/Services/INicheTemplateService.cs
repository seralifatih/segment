using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface INicheTemplateService
    {
        IReadOnlyList<NicheProjectTemplate> GetBuiltInTemplates();
        ProjectNicheConfiguration CreateProjectFromTemplate(string templateId, string projectProfileName, string targetLanguage);
        void ExportPack(string filePath, string projectProfileName, string exportedByUserId, string packName);
        NichePackImportResult ImportPack(string filePath, string projectProfileName, string targetLanguage, NichePackConflictMode conflictMode);
        bool TryGetProjectConfiguration(string projectProfileName, out ProjectNicheConfiguration configuration);
        void SaveProjectConfiguration(ProjectNicheConfiguration configuration);
        string SerializePack(NichePackDocument pack);
        NichePackDocument DeserializePack(string json);
    }
}
