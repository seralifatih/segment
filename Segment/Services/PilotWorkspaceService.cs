using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class PilotWorkspaceService : IPilotWorkspaceService, IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<PilotWorkspace> _workspaces;

        public PilotWorkspaceService(string? basePath = null)
        {
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");

            Directory.CreateDirectory(resolvedBasePath);
            string dbPath = Path.Combine(resolvedBasePath, "partner_gtm.db");

            _database = new LiteDatabase($"Filename={dbPath};Connection=shared");
            _workspaces = _database.GetCollection<PilotWorkspace>("pilot_workspaces");
            _workspaces.EnsureIndex(x => x.Id, unique: true);
            _workspaces.EnsureIndex(x => x.AgencyName);
        }

        public PilotWorkspace CreateWorkspace(string agencyName, string ownerUserId, int seatLimit, IEnumerable<string>? partnerTags = null)
        {
            if (string.IsNullOrWhiteSpace(agencyName))
            {
                throw new ArgumentException("Agency name is required.", nameof(agencyName));
            }

            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                throw new ArgumentException("Owner user ID is required.", nameof(ownerUserId));
            }

            lock (_syncRoot)
            {
                var workspace = new PilotWorkspace
                {
                    AgencyName = agencyName.Trim(),
                    OwnerUserId = ownerUserId.Trim(),
                    SeatLimit = Math.Max(1, seatLimit),
                    SharedGlossaryProfileName = $"Pilot-{SanitizeProfileName(agencyName)}",
                    PartnerTags = (partnerTags ?? Array.Empty<string>())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    CreatedAtUtc = DateTime.UtcNow
                };

                _workspaces.Insert(workspace);
                return workspace;
            }
        }

        public PilotWorkspace GetWorkspace(string workspaceId)
        {
            var workspace = _workspaces.FindById(workspaceId);
            if (workspace == null)
            {
                throw new InvalidOperationException($"Pilot workspace not found: {workspaceId}");
            }

            return workspace;
        }

        public PilotSeatInvite InviteSeat(string workspaceId, string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            lock (_syncRoot)
            {
                var workspace = GetWorkspace(workspaceId);
                string normalizedEmail = email.Trim().ToLowerInvariant();
                var existing = workspace.SeatInvites.FirstOrDefault(x =>
                    string.Equals(x.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    if (existing.Status == PilotSeatInviteStatus.Revoked)
                    {
                        existing.Status = PilotSeatInviteStatus.Pending;
                        existing.InvitedAtUtc = DateTime.UtcNow;
                        existing.AcceptedAtUtc = null;
                        _workspaces.Update(workspace);
                    }

                    return existing;
                }

                int activeInviteCount = workspace.SeatInvites.Count(x =>
                    x.Status == PilotSeatInviteStatus.Pending || x.Status == PilotSeatInviteStatus.Accepted);

                if (activeInviteCount >= workspace.SeatLimit)
                {
                    throw new InvalidOperationException("Seat limit reached for this pilot workspace.");
                }

                var invite = new PilotSeatInvite
                {
                    Email = normalizedEmail,
                    InvitedAtUtc = DateTime.UtcNow,
                    Status = PilotSeatInviteStatus.Pending
                };

                workspace.SeatInvites.Add(invite);
                _workspaces.Update(workspace);
                return invite;
            }
        }

        public void AcceptSeatInvite(string workspaceId, string email)
        {
            lock (_syncRoot)
            {
                var workspace = GetWorkspace(workspaceId);
                string normalizedEmail = email.Trim().ToLowerInvariant();
                var invite = workspace.SeatInvites.FirstOrDefault(x =>
                    string.Equals(x.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

                if (invite == null)
                {
                    throw new InvalidOperationException("Seat invite was not found.");
                }

                invite.Status = PilotSeatInviteStatus.Accepted;
                invite.AcceptedAtUtc = DateTime.UtcNow;
                _workspaces.Update(workspace);
            }
        }

        public int BootstrapSharedGlossary(string workspaceId, IReadOnlyList<TermEntry>? seedTerms = null)
        {
            lock (_syncRoot)
            {
                var workspace = GetWorkspace(workspaceId);
                GlossaryService.GetOrCreateProfile(workspace.SharedGlossaryProfileName);

                var terms = seedTerms?.ToList() ?? CreateDefaultSeedTerms();
                int inserted = GlossaryService.AddTerms(terms, isGlobal: false);

                workspace.SharedGlossaryBootstrappedAtUtc = DateTime.UtcNow;
                workspace.SharedGlossaryTermCount += inserted;
                _workspaces.Update(workspace);

                return inserted;
            }
        }

        public void RecordKpiSample(string workspaceId, PilotWorkspaceKpiSample sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            lock (_syncRoot)
            {
                var workspace = GetWorkspace(workspaceId);
                workspace.KpiSamples.Add(sample);
                workspace.KpiSamples = workspace.KpiSamples
                    .OrderBy(x => x.CapturedAtUtc)
                    .TakeLast(200)
                    .ToList();
                _workspaces.Update(workspace);
            }
        }

        public PilotWorkspaceKpiDashboard GetKpiDashboard(string workspaceId)
        {
            var workspace = GetWorkspace(workspaceId);
            var samples = workspace.KpiSamples;
            int accepted = workspace.SeatInvites.Count(x => x.Status == PilotSeatInviteStatus.Accepted);
            int invited = workspace.SeatInvites.Count(x => x.Status != PilotSeatInviteStatus.Revoked);

            if (samples.Count == 0)
            {
                return new PilotWorkspaceKpiDashboard
                {
                    WorkspaceId = workspace.Id,
                    SeatLimit = workspace.SeatLimit,
                    InvitedSeatCount = invited,
                    AcceptedSeatCount = accepted,
                    SeatUtilizationRate = workspace.SeatLimit == 0 ? 0 : (double)accepted / workspace.SeatLimit
                };
            }

            return new PilotWorkspaceKpiDashboard
            {
                WorkspaceId = workspace.Id,
                SeatLimit = workspace.SeatLimit,
                InvitedSeatCount = invited,
                AcceptedSeatCount = accepted,
                SeatUtilizationRate = workspace.SeatLimit == 0 ? 0 : (double)accepted / workspace.SeatLimit,
                AverageRetention30d = samples.Average(x => x.Retention30d),
                AverageTrialToPaidConversion = samples.Average(x => x.TrialToPaidConversion),
                AverageP95LatencyMs = samples.Average(x => x.P95LatencyMs),
                AverageTermViolationRate = samples.Average(x => x.TermViolationRate),
                TotalSamples = samples.Count
            };
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private static List<TermEntry> CreateDefaultSeedTerms()
        {
            return new List<TermEntry>
            {
                new() { Source = "indemnification", Target = "tazminat", Context = "legal" },
                new() { Source = "governing law", Target = "uygulanacak hukuk", Context = "legal" },
                new() { Source = "force majeure", Target = "mucbir sebep", Context = "legal" },
                new() { Source = "non-disclosure agreement", Target = "gizlilik sozlesmesi", Context = "legal" },
                new() { Source = "severability", Target = "bolunebilirlik", Context = "legal" }
            };
        }

        private static string SanitizeProfileName(string value)
        {
            var chars = value
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                .ToArray();

            if (chars.Length == 0)
            {
                return "Workspace";
            }

            return new string(chars);
        }
    }
}
