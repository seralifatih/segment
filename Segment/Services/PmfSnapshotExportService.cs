using System;
using System.Globalization;
using System.IO;
using System.Text;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class PmfSnapshotExportService : IPmfSnapshotExportService
    {
        public void ExportWeeklyCsv(PmfDashboardSnapshot snapshot, GateDecisionResult decision, string filePath)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("CSV path is required.", nameof(filePath));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            var sb = new StringBuilder();
            sb.AppendLine("Metric,Value");
            sb.AppendLine($"WindowStartUtc,{snapshot.WindowStartUtc:O}");
            sb.AppendLine($"WindowEndUtc,{snapshot.WindowEndUtc:O}");
            sb.AppendLine($"DAU,{snapshot.Dau}");
            sb.AppendLine($"WAU,{snapshot.Wau}");
            sb.AppendLine($"SegmentsPerDay,{Format(snapshot.SegmentsPerDay)}");
            sb.AppendLine($"Week4Retention,{Format(snapshot.RetentionWeek4)}");
            sb.AppendLine($"GlossaryReuseRate,{Format(snapshot.GlossaryReuseRate)}");
            sb.AppendLine($"TerminologyViolationRate,{Format(snapshot.TerminologyViolationRate)}");
            sb.AppendLine($"P50LatencyMs,{Format(snapshot.P50LatencyMs)}");
            sb.AppendLine($"P95LatencyMs,{Format(snapshot.P95LatencyMs)}");
            sb.AppendLine($"PilotToPaidConversion,{Format(snapshot.PilotToPaidConversion)}");
            sb.AppendLine($"ChurnRate,{Format(snapshot.ChurnRate)}");
            sb.AppendLine($"GateRecommendation,{decision.Recommendation}");
            sb.AppendLine($"GateReason,{EscapeCsv(decision.Reason)}");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportWeeklyPdf(PmfDashboardSnapshot snapshot, GateDecisionResult decision, string filePath)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("PDF path is required.", nameof(filePath));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            string[] lines =
            {
                "PMF Weekly Snapshot",
                $"Window: {snapshot.WindowStartUtc:yyyy-MM-dd} to {snapshot.WindowEndUtc:yyyy-MM-dd}",
                $"DAU: {snapshot.Dau}, WAU: {snapshot.Wau}",
                $"Segments/Day: {snapshot.SegmentsPerDay:F2}",
                $"Week-4 Retention: {snapshot.RetentionWeek4:P2}",
                $"Glossary Reuse: {snapshot.GlossaryReuseRate:P2}",
                $"Terminology Violation Rate: {snapshot.TerminologyViolationRate:P2}",
                $"Latency P50/P95: {snapshot.P50LatencyMs:F0} / {snapshot.P95LatencyMs:F0} ms",
                $"Pilot->Paid Conversion: {snapshot.PilotToPaidConversion:P2}",
                $"Churn: {snapshot.ChurnRate:P2}",
                $"Gate Recommendation: {decision.Recommendation}",
                $"Reason: {decision.Reason}"
            };

            WriteSimplePdf(filePath, lines);
        }

        private static string Format(double value) => value.ToString("F6", CultureInfo.InvariantCulture);

        private static string EscapeCsv(string value)
        {
            string normalized = value ?? string.Empty;
            if (normalized.Contains(",") || normalized.Contains("\"") || normalized.Contains("\n"))
            {
                return "\"" + normalized.Replace("\"", "\"\"") + "\"";
            }

            return normalized;
        }

        private static void WriteSimplePdf(string filePath, string[] lines)
        {
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
                string escaped = EscapePdf(lines[i]);
                if (i == 0) sb.Append($"({escaped}) Tj ");
                else sb.Append($"0 -18 Td ({escaped}) Tj ");
            }

            sb.Append("ET");
            return sb.ToString();
        }

        private static string EscapePdf(string text)
        {
            return (text ?? string.Empty).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }
    }
}
