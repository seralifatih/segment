using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Segment.App.Models;

namespace Segment.App.Services
{
    public static class TmxExportService
    {
        public static void Export(string filePath, IReadOnlyList<TermEntry> terms, string sourceLanguage, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Export path is required.", nameof(filePath));
            string fullPath = Path.GetFullPath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            string srcCode = NormalizeLanguage(sourceLanguage);
            string trgCode = NormalizeLanguage(targetLanguage);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false
            };

            using var writer = XmlWriter.Create(fullPath, settings);
            writer.WriteStartDocument();
            writer.WriteStartElement("tmx");
            writer.WriteAttributeString("version", "1.4");
            writer.WriteStartElement("header");
            writer.WriteAttributeString("creationtool", "Segment");
            writer.WriteAttributeString("srclang", srcCode);
            writer.WriteEndElement();
            writer.WriteStartElement("body");

            foreach (var term in (terms ?? Array.Empty<TermEntry>()).Where(x => x != null && !string.IsNullOrWhiteSpace(x.Source) && !string.IsNullOrWhiteSpace(x.Target)))
            {
                writer.WriteStartElement("tu");

                writer.WriteStartElement("tuv");
                writer.WriteAttributeString("xml", "lang", null, srcCode);
                writer.WriteElementString("seg", term.Source.Trim());
                writer.WriteEndElement();

                writer.WriteStartElement("tuv");
                writer.WriteAttributeString("xml", "lang", null, trgCode);
                writer.WriteElementString("seg", term.Target.Trim());
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private static string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return "en";
            string lower = language.Trim().ToLowerInvariant();
            return lower switch
            {
                "english" => "en",
                "turkish" => "tr",
                "german" => "de",
                "french" => "fr",
                "spanish" => "es",
                "russian" => "ru",
                "japanese" => "ja",
                "italian" => "it",
                "chinese" => "zh",
                "arabic" => "ar",
                _ => lower
            };
        }
    }
}
