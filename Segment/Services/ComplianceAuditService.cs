using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class ComplianceAuditService
    {
        private static readonly object SyncRoot = new();
        private static ComplianceAuditService? _defaultInstance;

        private readonly string _basePath;
        private readonly string _auditLogPath;

        public ComplianceAuditService(string? basePath = null)
        {
            _basePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
            Directory.CreateDirectory(_basePath);
            _auditLogPath = Path.Combine(_basePath, "compliance_audit.jsonl");
        }

        public static ComplianceAuditService Default
        {
            get
            {
                _defaultInstance ??= new ComplianceAuditService();
                return _defaultInstance;
            }
        }

        public void Record(ComplianceAuditRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            record.Id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("N") : record.Id.Trim();
            record.CapturedAtUtc = record.CapturedAtUtc == default ? DateTime.UtcNow : record.CapturedAtUtc.ToUniversalTime();
            record.AccountId = record.AccountId?.Trim() ?? string.Empty;
            record.Decision = record.Decision?.Trim() ?? string.Empty;
            record.ActiveMode = record.ActiveMode?.Trim() ?? string.Empty;
            record.ProviderRoute = record.ProviderRoute?.Trim() ?? string.Empty;
            record.RetentionPolicySummary = record.RetentionPolicySummary?.Trim() ?? string.Empty;
            record.Details = record.Details?.Trim() ?? string.Empty;

            string line = JsonSerializer.Serialize(record);
            lock (SyncRoot)
            {
                File.AppendAllText(_auditLogPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }

        public IReadOnlyList<ComplianceAuditRecord> GetRecords()
        {
            lock (SyncRoot)
            {
                if (!File.Exists(_auditLogPath))
                {
                    return Array.Empty<ComplianceAuditRecord>();
                }

                var records = new List<ComplianceAuditRecord>();
                foreach (string line in File.ReadLines(_auditLogPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var record = JsonSerializer.Deserialize<ComplianceAuditRecord>(line);
                        if (record != null)
                        {
                            records.Add(record);
                        }
                    }
                    catch
                    {
                        // Skip malformed lines.
                    }
                }

                return records.OrderBy(x => x.CapturedAtUtc).ToList();
            }
        }

        public void ExportJsonl(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Export path is required.", nameof(filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

            lock (SyncRoot)
            {
                if (File.Exists(_auditLogPath))
                {
                    File.Copy(_auditLogPath, filePath, overwrite: true);
                }
                else
                {
                    File.WriteAllText(filePath, string.Empty, Encoding.UTF8);
                }
            }
        }

        public void ExportCsv(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Export path is required.", nameof(filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

            var records = GetRecords();
            var sb = new StringBuilder();
            sb.AppendLine("Id,CapturedAtUtc,EventType,AccountId,Decision,ActiveMode,ProviderRoute,RetentionPolicySummary,Details,MetadataJson");

            foreach (var record in records)
            {
                string metadataJson = JsonSerializer.Serialize(record.Metadata ?? new Dictionary<string, string>());
                sb.AppendLine(
                    $"{Escape(record.Id)}," +
                    $"{record.CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture)}," +
                    $"{record.EventType}," +
                    $"{Escape(record.AccountId)}," +
                    $"{Escape(record.Decision)}," +
                    $"{Escape(record.ActiveMode)}," +
                    $"{Escape(record.ProviderRoute)}," +
                    $"{Escape(record.RetentionPolicySummary)}," +
                    $"{Escape(record.Details)}," +
                    $"{Escape(metadataJson)}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string Escape(string value)
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
