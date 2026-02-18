using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class ComplianceAuditServiceTests
    {
        [Fact]
        public void Record_Should_Persist_Complete_Audit_Evidence_For_All_Event_Types()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "SegmentComplianceAuditTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);

            try
            {
                var service = new ComplianceAuditService(basePath);

                service.Record(new ComplianceAuditRecord
                {
                    EventType = ComplianceAuditEventType.RoutingDecision,
                    AccountId = "acct-1",
                    Decision = "allowed",
                    ActiveMode = "Standard",
                    ProviderRoute = "Google",
                    RetentionPolicySummary = "Local + opt-in telemetry",
                    Details = "Routing permitted.",
                    Metadata = new Dictionary<string, string> { ["requested_provider"] = "Google" }
                });

                service.Record(new ComplianceAuditRecord
                {
                    EventType = ComplianceAuditEventType.GuardrailOverride,
                    AccountId = "acct-1",
                    Decision = "applied",
                    ActiveMode = "Confidential Local-Only",
                    ProviderRoute = "Ollama",
                    RetentionPolicySummary = "Local only",
                    Details = "Frozen glossary override enabled.",
                    Metadata = new Dictionary<string, string> { ["profile"] = "Litigation" }
                });

                service.Record(new ComplianceAuditRecord
                {
                    EventType = ComplianceAuditEventType.GlossaryConflictDecision,
                    AccountId = "acct-1",
                    Decision = "confirm_overwrite",
                    ActiveMode = "Standard",
                    ProviderRoute = "Custom",
                    RetentionPolicySummary = "30-day cloud logs",
                    Details = "Conflict overwrite accepted.",
                    Metadata = new Dictionary<string, string> { ["term"] = "indemnity" }
                });

                var records = service.GetRecords();

                records.Should().HaveCount(3);
                records.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Id));
                records.Should().OnlyContain(x => x.CapturedAtUtc != default);
                records.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.AccountId));
                records.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Decision));
                records.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.ActiveMode));
                records.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.ProviderRoute));
                records.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.RetentionPolicySummary));
                records.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Details));
                records.Should().OnlyContain(x => x.Metadata != null && x.Metadata.Count > 0);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(basePath))
                    {
                        Directory.Delete(basePath, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }
    }
}
