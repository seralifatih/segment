using System;
using System.Globalization;
using System.IO;
using System.Text;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class CoBrandedExportService : ICoBrandedExportService
    {
        public void ExportPilotOutcomeSummaryCsv(PilotRoiReport report, CoBrandedExportOptions options, string filePath)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("CSV file path is required.", nameof(filePath));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            var sb = new StringBuilder();
            sb.AppendLine("Field,Value");
            AppendBranding(sb, options);
            sb.AppendLine($"SessionId,{EscapeCsv(report.SessionId)}");
            sb.AppendLine($"GeneratedAtUtc,{report.GeneratedAtUtc:O}");
            sb.AppendLine($"TimeSavedPercentage,{report.TimeSavedPercentage.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"ViolationReductionPercentage,{report.ViolationReductionPercentage.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"ConfidenceSummary,{EscapeCsv(report.ConfidenceSummary)}");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportPilotOutcomeSummaryPdf(PilotRoiReport report, CoBrandedExportOptions options, string filePath)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            WriteSimplePdf(filePath, new[]
            {
                "Pilot Outcome Summary",
                BuildBrandLine(options),
                $"Session: {report.SessionId}",
                $"Generated: {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC",
                $"Time Saved: {report.TimeSavedPercentage:F2}%",
                $"Violation Reduction: {report.ViolationReductionPercentage:F2}%",
                $"Confidence: {report.ConfidenceSummary}"
            });
        }

        public void ExportGlossaryQualityReportCsv(GlossaryQualityReport report, CoBrandedExportOptions options, string filePath)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("CSV file path is required.", nameof(filePath));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            var sb = new StringBuilder();
            sb.AppendLine("Field,Value");
            AppendBranding(sb, options);
            sb.AppendLine($"WorkspaceId,{EscapeCsv(report.WorkspaceId)}");
            sb.AppendLine($"TotalTerms,{report.TotalTerms}");
            sb.AppendLine($"ConfirmedTerms,{report.ConfirmedTerms}");
            sb.AppendLine($"RecentlyUsedTerms,{report.RecentlyUsedTerms}");
            sb.AppendLine($"ConfirmationRate,{report.ConfirmationRate.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"RecentUsageRate,{report.RecentUsageRate.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"EstimatedViolationRate,{report.EstimatedViolationRate.ToString("F4", CultureInfo.InvariantCulture)}");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportGlossaryQualityReportPdf(GlossaryQualityReport report, CoBrandedExportOptions options, string filePath)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            WriteSimplePdf(filePath, new[]
            {
                "Glossary Quality Report",
                BuildBrandLine(options),
                $"Workspace: {report.WorkspaceId}",
                $"Total Terms: {report.TotalTerms}",
                $"Confirmed Terms: {report.ConfirmedTerms}",
                $"Recently Used Terms: {report.RecentlyUsedTerms}",
                $"Confirmation Rate: {report.ConfirmationRate:P2}",
                $"Recent Usage Rate: {report.RecentUsageRate:P2}",
                $"Estimated Violation Rate: {report.EstimatedViolationRate:P2}"
            });
        }

        private static string BuildBrandLine(CoBrandedExportOptions options)
        {
            options ??= new CoBrandedExportOptions();
            string partner = string.IsNullOrWhiteSpace(options.PartnerName) ? "Segment Partner" : options.PartnerName.Trim();
            string tagline = string.IsNullOrWhiteSpace(options.PartnerTagline) ? "Co-branded pilot template" : options.PartnerTagline.Trim();
            string workspace = string.IsNullOrWhiteSpace(options.WorkspaceName) ? "N/A" : options.WorkspaceName.Trim();
            return $"{partner} | {tagline} | Workspace: {workspace}";
        }

        private static void AppendBranding(StringBuilder sb, CoBrandedExportOptions options)
        {
            options ??= new CoBrandedExportOptions();
            sb.AppendLine($"PartnerName,{EscapeCsv(options.PartnerName)}");
            sb.AppendLine($"PartnerTagline,{EscapeCsv(options.PartnerTagline)}");
            sb.AppendLine($"WorkspaceName,{EscapeCsv(options.WorkspaceName)}");
            sb.AppendLine($"GeneratedBy,{EscapeCsv(options.GeneratedBy)}");
        }

        private static void WriteSimplePdf(string filePath, string[] lines)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("PDF file path is required.", nameof(filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

            string content = BuildPdfContentStream(lines);
            byte[] contentBytes = Encoding.ASCII.GetBytes(content);

            string obj1 = "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n";
            string obj2 = "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n";
            string obj3 = "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj\n";
            string obj4 = "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n";
            string obj5Header = $"5 0 obj << /Length {contentBytes.Length} >> stream\n";
            string obj5Footer = "\nendstream endobj\n";

            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);

            writer.Write("%PDF-1.4\n");
            writer.Flush();
            long xref1 = ms.Position; writer.Write(obj1); writer.Flush();
            long xref2 = ms.Position; writer.Write(obj2); writer.Flush();
            long xref3 = ms.Position; writer.Write(obj3); writer.Flush();
            long xref4 = ms.Position; writer.Write(obj4); writer.Flush();
            long xref5 = ms.Position; writer.Write(obj5Header); writer.Flush();
            ms.Write(contentBytes, 0, contentBytes.Length);
            writer.Write(obj5Footer); writer.Flush();

            long xrefStart = ms.Position;
            writer.Write("xref\n0 6\n");
            writer.Write("0000000000 65535 f \n");
            writer.Write($"{xref1:D10} 00000 n \n");
            writer.Write($"{xref2:D10} 00000 n \n");
            writer.Write($"{xref3:D10} 00000 n \n");
            writer.Write($"{xref4:D10} 00000 n \n");
            writer.Write($"{xref5:D10} 00000 n \n");
            writer.Write("trailer << /Size 6 /Root 1 0 R >>\nstartxref\n");
            writer.Write($"{xrefStart}\n%%EOF");
            writer.Flush();

            File.WriteAllBytes(filePath, ms.ToArray());
        }

        private static string BuildPdfContentStream(string[] lines)
        {
            var sb = new StringBuilder();
            sb.Append("BT /F1 12 Tf 50 790 Td ");
            for (int i = 0; i < lines.Length; i++)
            {
                string escaped = EscapePdfText(lines[i]);
                if (i == 0) sb.Append($"({escaped}) Tj ");
                else sb.Append($"0 -18 Td ({escaped}) Tj ");
            }

            sb.Append("ET");
            return sb.ToString();
        }

        private static string EscapePdfText(string text)
        {
            return (text ?? string.Empty).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        private static string EscapeCsv(string value)
        {
            string normalized = value ?? string.Empty;
            if (normalized.Contains(",") || normalized.Contains("\"") || normalized.Contains("\n"))
            {
                return "\"" + normalized.Replace("\"", "\"\"") + "\"";
            }

            return normalized;
        }
    }
}
