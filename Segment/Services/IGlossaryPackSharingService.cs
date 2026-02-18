using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IGlossaryPackSharingService
    {
        GlossaryPackMetadata ExportLegalGlossaryPack(string filePath, string exportedByUserId, string packName, string referralCode, bool isGlobal = true);
        GlossaryPackImportResult ImportGlossaryPack(string filePath, string importedByUserId, bool isGlobal = true);
    }
}
