using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class TextRedactionService : ITextRedactionService
    {
        private static readonly Regex EmailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);
        private static readonly Regex IdRegex = new(@"\b[A-Z]{2,}-?\d{4,}\b", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new(@"\b\d+(?:[.,]\d+)?\b", RegexOptions.Compiled);
        private static readonly Regex NameRegex = new(@"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+){1,2}\b", RegexOptions.Compiled);

        public RedactionResult Redact(string input)
        {
            string text = input ?? string.Empty;
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            int counter = 1;

            text = ReplaceWithToken(text, EmailRegex, "EMAIL", replacements, ref counter);
            text = ReplaceWithToken(text, IdRegex, "ID", replacements, ref counter);
            text = ReplaceWithToken(text, NumberRegex, "NUM", replacements, ref counter);
            text = ReplaceWithToken(text, NameRegex, "NAME", replacements, ref counter);

            return new RedactionResult
            {
                RedactedText = text,
                TokenToOriginalMap = replacements
            };
        }

        public string Restore(string redactedText, RedactionResult mapping)
        {
            string output = redactedText ?? string.Empty;
            if (mapping?.TokenToOriginalMap == null || mapping.TokenToOriginalMap.Count == 0)
            {
                return output;
            }

            foreach (var pair in mapping.TokenToOriginalMap.OrderByDescending(x => x.Key.Length))
            {
                output = output.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
            }

            return output;
        }

        private static string ReplaceWithToken(
            string input,
            Regex regex,
            string tokenPrefix,
            Dictionary<string, string> map,
            ref int counter)
        {
            int localCounter = counter;
            string result = regex.Replace(input, match =>
            {
                string token = $"[{tokenPrefix}_{localCounter:000}]";
                localCounter++;
                if (!map.ContainsKey(token))
                {
                    map[token] = match.Value;
                }

                return token;
            });

            counter = localCounter;
            return result;
        }
    }
}
