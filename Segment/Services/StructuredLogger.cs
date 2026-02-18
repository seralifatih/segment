using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Segment.App.Services
{
    public class StructuredLogger
    {
        private readonly object _syncRoot = new();
        private readonly string _logPath;
        private readonly bool _enforceConsent;

        public StructuredLogger(string? basePath = null, bool enforceConsent = true)
        {
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
            Directory.CreateDirectory(resolvedBasePath);
            _logPath = Path.Combine(resolvedBasePath, "structured_events.jsonl");
            _enforceConsent = enforceConsent;
        }

        public void Info(string eventName, Dictionary<string, string>? fields = null)
        {
            Write("info", eventName, fields);
        }

        public void Error(string eventName, Exception exception, Dictionary<string, string>? fields = null)
        {
            var safeFields = fields ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            safeFields["error_type"] = exception.GetType().Name;
            safeFields["error_message"] = Scrub(exception.Message);
            Write("error", eventName, safeFields);
        }

        public string Scrub(string value)
        {
            return SensitiveDataRedactor.Redact(value ?? string.Empty);
        }

        private void Write(string level, string eventName, Dictionary<string, string>? fields)
        {
            if (_enforceConsent
                && string.Equals(level, "info", StringComparison.OrdinalIgnoreCase)
                && !SettingsService.Current.TelemetryUsageMetricsConsent)
            {
                return;
            }

            if (_enforceConsent
                && string.Equals(level, "error", StringComparison.OrdinalIgnoreCase)
                && !SettingsService.Current.TelemetryCrashDiagnosticsConsent)
            {
                return;
            }

            var payload = new Dictionary<string, object>
            {
                ["ts_utc"] = DateTime.UtcNow,
                ["level"] = level,
                ["event"] = string.IsNullOrWhiteSpace(eventName) ? "unspecified" : eventName.Trim()
            };

            bool minimize = SettingsService.Current.MinimizeDiagnosticLogging
                || string.Equals(SettingsService.Current.ConfidentialityMode, "LocalOnly", StringComparison.OrdinalIgnoreCase);

            if (fields != null && !minimize)
            {
                var safe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in fields)
                {
                    safe[pair.Key] = Scrub(pair.Value ?? string.Empty);
                }
                payload["fields"] = safe;
            }

            string line = JsonSerializer.Serialize(payload);
            lock (_syncRoot)
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
    }
}
