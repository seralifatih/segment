using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public static class TmxImportService
    {
        public static List<TermEntry> Import(string filePath, string? targetLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("TMX file not found.", filePath);

            var terms = new List<TermEntry>();
            string? sourceLangCode = null;
            string? targetLangCode = NormalizeLangCode(targetLanguage);

            // Use XmlReader for streaming (memory-efficient for large files)
            using (var reader = XmlReader.Create(filePath, new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true
            }))
            {
                // Read header to get source language
                if (reader.ReadToFollowing("header"))
                {
                    string? headerSourceLang = reader.GetAttribute("srclang");
                    sourceLangCode = NormalizeLangCode(headerSourceLang);
                }

                // Process each <tu> element one at a time (streaming)
                while (reader.ReadToFollowing("tu"))
                {
                    // Read just this single TU into memory using a subtree reader
                    using (var tuReader = reader.ReadSubtree())
                    {
                        tuReader.MoveToContent();
                        var tu = (XElement)XElement.ReadFrom(tuReader);

                        // Use existing LINQ logic for this single TU
                        var tuvs = tu.Descendants("tuv")
                                     .Select(tuv => new TuvEntry
                                     {
                                         Lang = NormalizeLangCode((string?)tuv.Attribute(XNamespace.Xml + "lang") ??
                                                                  (string?)tuv.Attribute("lang")),
                                         Text = (tuv.Element("seg")?.Value ?? "").Trim()
                                     })
                                     .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                                     .ToList();

                        if (tuvs.Count < 2) continue;

                        string? source = null;
                        string? target = null;

                        if (!string.IsNullOrWhiteSpace(targetLangCode))
                        {
                            target = tuvs.FirstOrDefault(t => LangMatches(t.Lang, targetLangCode))?.Text;
                        }

                        if (!string.IsNullOrWhiteSpace(sourceLangCode))
                        {
                            source = tuvs.FirstOrDefault(t => LangMatches(t.Lang, sourceLangCode))?.Text;
                        }
                        else if (!string.IsNullOrWhiteSpace(targetLangCode))
                        {
                            source = tuvs.FirstOrDefault(t => !LangMatches(t.Lang, targetLangCode))?.Text;
                        }

                        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                        {
                            source = tuvs[0].Text;
                            target = tuvs[1].Text;
                        }

                        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;

                        terms.Add(new TermEntry
                        {
                            Source = source,
                            Target = target,
                            Context = "tmx",
                            CreatedBy = "tmx-import",
                            CreatedAt = DateTime.Now,
                            LastUsed = DateTime.Now,
                            UsageCount = 0,
                            IsUserConfirmed = true,
                            SourceLanguage = string.IsNullOrWhiteSpace(sourceLangCode) ? "English" : sourceLangCode,
                            TargetLanguage = string.IsNullOrWhiteSpace(targetLangCode) ? (targetLanguage ?? "Turkish") : targetLangCode
                        });
                    }
                }
            }

            return terms;
        }

        private static bool LangMatches(string? lang, string? targetCode)
        {
            if (string.IsNullOrWhiteSpace(lang) || string.IsNullOrWhiteSpace(targetCode)) return false;
            if (lang == targetCode) return true;
            return lang.StartsWith(targetCode + "-", StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeLangCode(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return null;
            string normalized = lang.Trim().ToLowerInvariant();

            if (LanguageNameToCode.TryGetValue(normalized, out var code))
                return code;

            if (normalized.Length <= 8)
                return normalized;

            return normalized;
        }

        private static readonly Dictionary<string, string> LanguageNameToCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["turkish"] = "tr",
            ["english"] = "en",
            ["german"] = "de",
            ["french"] = "fr",
            ["spanish"] = "es",
            ["russian"] = "ru",
            ["japanese"] = "ja",
            ["italian"] = "it",
            ["chinese"] = "zh",
            ["arabic"] = "ar"
        };

        private class TuvEntry
        {
            public string? Lang { get; set; }
            public string Text { get; set; } = "";
        }
    }
}
