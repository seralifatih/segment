using System;
using System.IO;
using System.Text;

namespace Segment.App.Services
{
    internal static class SimplePdfWriter
    {
        public static void WriteSinglePage(string filePath, string title, string content)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("PDF file path is required.", nameof(filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

            string[] lines = BuildLines(title, content);
            string stream = BuildContentStream(lines);
            byte[] contentBytes = Encoding.ASCII.GetBytes(stream);

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

        private static string[] BuildLines(string title, string content)
        {
            string normalizedTitle = title ?? string.Empty;
            string normalizedContent = content ?? string.Empty;
            string[] bodyLines = normalizedContent
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');

            string[] lines = new string[1 + bodyLines.Length];
            lines[0] = normalizedTitle;
            for (int i = 0; i < bodyLines.Length; i++)
            {
                lines[i + 1] = bodyLines[i];
            }

            return lines;
        }

        private static string BuildContentStream(string[] lines)
        {
            var sb = new StringBuilder();
            sb.Append("BT /F1 11 Tf 50 790 Td ");
            for (int i = 0; i < lines.Length; i++)
            {
                string escaped = EscapePdfText(lines[i]);
                if (i == 0)
                {
                    sb.Append($"({escaped}) Tj ");
                }
                else
                {
                    sb.Append($"0 -14 Td ({escaped}) Tj ");
                }
            }

            sb.Append("ET");
            return sb.ToString();
        }

        private static string EscapePdfText(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }
    }
}
