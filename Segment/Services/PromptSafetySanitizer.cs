using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Segment.App.Services
{
    public static class PromptSafetySanitizer
    {
        private static readonly Regex ControlCharRegex = new(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F]+", RegexOptions.Compiled);
        private static readonly Regex RolePrefixRegex = new(@"(?im)^\s*(system|assistant|user|developer)\s*:", RegexOptions.Compiled);
        private static readonly Regex InstructionRegex = new(
            @"(?i)\b(ignore\s+previous|disregard\s+above|override\s+policy|system\s+prompt|developer\s+message|execute\s+command|tool\s+call|jailbreak|do\s+not\s+translate)\b",
            RegexOptions.Compiled);
        private static readonly Regex FenceRegex = new(@"```+", RegexOptions.Compiled);

        public static string SanitizeUntrustedSourceText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            string text = CollapseWhitespace(ControlCharRegex.Replace(input, " "));
            text = FenceRegex.Replace(text, "` ` `");
            text = RolePrefixRegex.Replace(text, m => $"[{m.Groups[1].Value.ToUpperInvariant()}_TAG]:");
            return text.Trim();
        }

        public static string SanitizeGlossaryConstraint(string input)
        {
            string text = SanitizeUntrustedSourceText(input);
            if (InstructionRegex.IsMatch(text))
            {
                text = InstructionRegex.Replace(text, "[blocked_instruction]");
            }

            return text.Trim(' ', '\'', '"');
        }

        public static IReadOnlyDictionary<string, string> SanitizeGlossaryConstraints(IReadOnlyDictionary<string, string> constraints)
        {
            var safe = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (constraints == null || constraints.Count == 0)
            {
                return safe;
            }

            foreach ((string source, string target) in constraints)
            {
                if (IsInstructionLike(source) || IsInstructionLike(target))
                {
                    continue;
                }

                string cleanSource = SanitizeGlossaryConstraint(source);
                string cleanTarget = SanitizeGlossaryConstraint(target);
                if (string.IsNullOrWhiteSpace(cleanSource) || string.IsNullOrWhiteSpace(cleanTarget))
                {
                    continue;
                }

                if (IsInstructionLike(cleanSource) || IsInstructionLike(cleanTarget)
                    || cleanSource.Contains("[blocked_instruction]", StringComparison.OrdinalIgnoreCase)
                    || cleanTarget.Contains("[blocked_instruction]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                safe[cleanSource] = cleanTarget;
            }

            return safe;
        }

        public static bool IsInstructionLike(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            return InstructionRegex.IsMatch(input) || RolePrefixRegex.IsMatch(input);
        }

        public static double ComputeReputationScore(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return 0;
            }

            string text = input.Trim();
            double score = 1.0;
            if (IsInstructionLike(text))
            {
                score -= 0.6;
            }

            if (text.Length > 64)
            {
                score -= 0.2;
            }

            if (text.Count(char.IsPunctuation) > text.Length / 3)
            {
                score -= 0.2;
            }

            return Math.Max(0, Math.Min(1, score));
        }

        private static string CollapseWhitespace(string input)
        {
            return Regex.Replace(input ?? string.Empty, @"\s+", " ");
        }
    }
}
