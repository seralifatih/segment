using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Segment.App.Services
{
    public sealed class ComplianceAuditIntegrityReport
    {
        public bool Success { get; set; }
        public int RecordCount { get; set; }
        public int CheckpointCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class ComplianceAuditCheckpoint
    {
        public int RecordIndex { get; set; }
        public string ChainHash { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }

    public class ComplianceAuditIntegrityService
    {
        private const string GenesisHash = "GENESIS";
        private readonly string _basePath;
        private readonly string _auditPath;
        private readonly string _checkpointPath;

        public ComplianceAuditIntegrityService(string? basePath = null)
        {
            _basePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
            Directory.CreateDirectory(_basePath);
            _auditPath = Path.Combine(_basePath, "compliance_audit.jsonl");
            _checkpointPath = Path.Combine(_basePath, "compliance_audit.checkpoints.json");
        }

        public ComplianceAuditIntegrityReport RebuildCheckpoints(int checkpointInterval = 100, string signingKey = "")
        {
            int safeInterval = Math.Max(1, checkpointInterval);
            var lines = ReadAuditLines();
            var checkpoints = BuildCheckpoints(lines, safeInterval, signingKey);
            File.WriteAllText(_checkpointPath, JsonSerializer.Serialize(checkpoints, new JsonSerializerOptions { WriteIndented = true }));

            return new ComplianceAuditIntegrityReport
            {
                Success = true,
                RecordCount = lines.Count,
                CheckpointCount = checkpoints.Count,
                Message = "Checkpoint chain rebuilt."
            };
        }

        public ComplianceAuditIntegrityReport Verify(int checkpointInterval = 100, string signingKey = "")
        {
            int safeInterval = Math.Max(1, checkpointInterval);
            var lines = ReadAuditLines();
            if (lines.Count == 0)
            {
                return new ComplianceAuditIntegrityReport
                {
                    Success = true,
                    RecordCount = 0,
                    CheckpointCount = 0,
                    Message = "No audit records found."
                };
            }

            if (!File.Exists(_checkpointPath))
            {
                return new ComplianceAuditIntegrityReport
                {
                    Success = false,
                    RecordCount = lines.Count,
                    Message = "Checkpoint file missing for non-empty audit log."
                };
            }

            var stored = JsonSerializer.Deserialize<List<ComplianceAuditCheckpoint>>(File.ReadAllText(_checkpointPath))
                ?? new List<ComplianceAuditCheckpoint>();
            if (stored.Count == 0)
            {
                return new ComplianceAuditIntegrityReport
                {
                    Success = false,
                    RecordCount = lines.Count,
                    CheckpointCount = 0,
                    Message = "Checkpoint file is empty for non-empty audit log."
                };
            }

            var expected = BuildCheckpoints(lines, safeInterval, signingKey);
            if (stored.Count != expected.Count)
            {
                return new ComplianceAuditIntegrityReport
                {
                    Success = false,
                    RecordCount = lines.Count,
                    CheckpointCount = stored.Count,
                    Message = "Checkpoint count mismatch."
                };
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (stored[i].RecordIndex != expected[i].RecordIndex ||
                    !string.Equals(stored[i].ChainHash, expected[i].ChainHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new ComplianceAuditIntegrityReport
                    {
                        Success = false,
                        RecordCount = lines.Count,
                        CheckpointCount = stored.Count,
                        Message = $"Checkpoint mismatch at index {i}."
                    };
                }

                if (!string.IsNullOrWhiteSpace(signingKey))
                {
                    if (!string.Equals(stored[i].Signature, expected[i].Signature, StringComparison.Ordinal))
                    {
                        return new ComplianceAuditIntegrityReport
                        {
                            Success = false,
                            RecordCount = lines.Count,
                            CheckpointCount = stored.Count,
                            Message = $"Checkpoint signature mismatch at index {i}."
                        };
                    }
                }
            }

            return new ComplianceAuditIntegrityReport
            {
                Success = true,
                RecordCount = lines.Count,
                CheckpointCount = stored.Count,
                Message = "Audit hash chain and checkpoint signatures verified."
            };
        }

        private List<string> ReadAuditLines()
        {
            if (!File.Exists(_auditPath))
            {
                return new List<string>();
            }

            return File.ReadLines(_auditPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }

        private static List<ComplianceAuditCheckpoint> BuildCheckpoints(IReadOnlyList<string> lines, int checkpointInterval, string signingKey)
        {
            var checkpoints = new List<ComplianceAuditCheckpoint>();
            string previous = GenesisHash;

            for (int i = 0; i < lines.Count; i++)
            {
                string current = ComputeSha256Hex($"{previous}|{lines[i]}");
                previous = current;

                bool isInterval = ((i + 1) % checkpointInterval) == 0;
                bool isLast = i == lines.Count - 1;
                if (!isInterval && !isLast)
                {
                    continue;
                }

                checkpoints.Add(new ComplianceAuditCheckpoint
                {
                    RecordIndex = i,
                    ChainHash = current,
                    Signature = string.IsNullOrWhiteSpace(signingKey) ? string.Empty : ComputeHmacHex(signingKey, $"{i}:{current}")
                });
            }

            return checkpoints;
        }

        private static string ComputeSha256Hex(string value)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string ComputeHmacHex(string key, string value)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
