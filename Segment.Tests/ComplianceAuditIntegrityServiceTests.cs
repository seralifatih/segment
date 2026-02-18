using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class ComplianceAuditIntegrityServiceTests
    {
        [Fact]
        [Trait("ReleaseGate", "Must")]
        public void Verify_Should_Pass_For_Untampered_Log_With_Checkpoints()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "SegmentAuditIntegrityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);

            try
            {
                var audit = new ComplianceAuditService(basePath);
                audit.Record(new ComplianceAuditRecord
                {
                    EventType = ComplianceAuditEventType.RoutingDecision,
                    AccountId = "acct-1",
                    Decision = "allowed",
                    Details = "route ok",
                    Metadata = new Dictionary<string, string> { ["route"] = "Google" }
                });
                audit.Record(new ComplianceAuditRecord
                {
                    EventType = ComplianceAuditEventType.GuardrailOverride,
                    AccountId = "acct-1",
                    Decision = "applied",
                    Details = "override",
                    Metadata = new Dictionary<string, string> { ["reason"] = "urgent" }
                });

                var integrity = new ComplianceAuditIntegrityService(basePath);
                integrity.RebuildCheckpoints(checkpointInterval: 1, signingKey: "release-key");
                var report = integrity.Verify(checkpointInterval: 1, signingKey: "release-key");

                report.Success.Should().BeTrue();
                report.RecordCount.Should().Be(2);
                report.CheckpointCount.Should().Be(2);
            }
            finally
            {
                TryDelete(basePath);
            }
        }

        [Fact]
        [Trait("ReleaseGate", "Must")]
        public void Verify_Should_Fail_When_Log_Is_Tampered_After_Checkpoint()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "SegmentAuditIntegrityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);

            try
            {
                var audit = new ComplianceAuditService(basePath);
                audit.Record(new ComplianceAuditRecord
                {
                    EventType = ComplianceAuditEventType.RoutingDecision,
                    AccountId = "acct-1",
                    Decision = "allowed",
                    Details = "route ok"
                });

                var integrity = new ComplianceAuditIntegrityService(basePath);
                integrity.RebuildCheckpoints(checkpointInterval: 1, signingKey: "release-key");

                string path = Path.Combine(basePath, "compliance_audit.jsonl");
                string[] lines = File.ReadAllLines(path);
                lines[0] = lines[0].Replace("allowed", "blocked", StringComparison.OrdinalIgnoreCase);
                File.WriteAllLines(path, lines);

                var report = integrity.Verify(checkpointInterval: 1, signingKey: "release-key");
                report.Success.Should().BeFalse();
                report.Message.Should().Contain("mismatch");
            }
            finally
            {
                TryDelete(basePath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }
    }
}
