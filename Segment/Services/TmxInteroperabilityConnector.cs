using System;
using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class TmxInteroperabilityConnector : IInteroperabilityConnector
    {
        public string ConnectorId => "tmx-file";
        public IReadOnlyList<string> SupportedFormats => new[] { "tmx" };

        public bool CanImport(string format) => string.Equals(format, "tmx", StringComparison.OrdinalIgnoreCase);
        public bool CanExport(string format) => string.Equals(format, "tmx", StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<TermEntry> ImportTerms(string format, string filePath, InteropTermTransferOptions options)
        {
            if (!CanImport(format)) throw new InvalidOperationException($"Format '{format}' is not supported by {ConnectorId}.");
            string target = options?.TargetLanguage ?? "Turkish";
            return TmxImportService.Import(filePath, target);
        }

        public void ExportTerms(string format, string filePath, IReadOnlyList<TermEntry> terms, InteropTermTransferOptions options)
        {
            if (!CanExport(format)) throw new InvalidOperationException($"Format '{format}' is not supported by {ConnectorId}.");
            string source = options?.SourceLanguage ?? "English";
            string target = options?.TargetLanguage ?? "Turkish";
            TmxExportService.Export(filePath, terms, source, target);
        }
    }
}
