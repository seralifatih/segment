using System;
using Segment.App.Models;

namespace Segment.App.Services
{
    public static class ClipboardSafetyService
    {
        public static ClipboardCollisionDecision EvaluateOverwrite(string expectedClipboardSnapshot, string currentClipboardSnapshot)
        {
            string expected = expectedClipboardSnapshot ?? string.Empty;
            string current = currentClipboardSnapshot ?? string.Empty;

            if (string.Equals(expected, current, StringComparison.Ordinal))
            {
                return new ClipboardCollisionDecision
                {
                    AllowOverwrite = true,
                    Reason = "Clipboard unchanged."
                };
            }

            return new ClipboardCollisionDecision
            {
                AllowOverwrite = false,
                Reason = "Clipboard changed after capture. Paste operation aborted to prevent accidental overwrite."
            };
        }
    }
}
