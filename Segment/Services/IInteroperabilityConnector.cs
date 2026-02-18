using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IInteroperabilityConnector
    {
        string ConnectorId { get; }
        IReadOnlyList<string> SupportedFormats { get; }
        bool CanImport(string format);
        bool CanExport(string format);
        IReadOnlyList<TermEntry> ImportTerms(string format, string filePath, InteropTermTransferOptions options);
        void ExportTerms(string format, string filePath, IReadOnlyList<TermEntry> terms, InteropTermTransferOptions options);
    }
}
