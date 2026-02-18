using System;
using System.IO;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class PilotWorkspaceIntegrationTests : IDisposable
    {
        private readonly string _basePath;
        private readonly string _glossaryBasePath;
        private readonly PilotWorkspaceService _workspaceService;
        private readonly AccountMetadataService _accountMetadataService;

        public PilotWorkspaceIntegrationTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentPilotWorkspaceIntegrationTests", Guid.NewGuid().ToString("N"));
            _glossaryBasePath = Path.Combine(_basePath, "glossary");
            Directory.CreateDirectory(_basePath);
            GlossaryService.InitializeForTests(_glossaryBasePath);
            _workspaceService = new PilotWorkspaceService(_basePath);
            _accountMetadataService = new AccountMetadataService(_basePath);
        }

        [Fact]
        public void Setup_Workspace_Should_Seed_Glossary_Manage_Seats_And_Tag_Partner()
        {
            var account = _accountMetadataService.SetPartnerTags(
                accountId: "agency-account-1",
                tags: new[] { "law-association", "webinar-partner" },
                displayName: "Agency Account 1");

            var workspace = _workspaceService.CreateWorkspace(
                agencyName: "Legal Agency One",
                ownerUserId: account.AccountId,
                seatLimit: 3,
                partnerTags: account.PartnerTags);

            int seededCount = _workspaceService.BootstrapSharedGlossary(workspace.Id);
            _workspaceService.InviteSeat(workspace.Id, "pilot1@legalagency.com");
            _workspaceService.InviteSeat(workspace.Id, "pilot2@legalagency.com");
            _workspaceService.AcceptSeatInvite(workspace.Id, "pilot1@legalagency.com");

            var stored = _workspaceService.GetWorkspace(workspace.Id);
            var dashboard = _workspaceService.GetKpiDashboard(workspace.Id);

            account.PartnerTags.Should().Contain("law-association");
            seededCount.Should().BeGreaterThan(0);
            stored.SharedGlossaryBootstrappedAtUtc.Should().NotBeNull();
            stored.SeatInvites.Should().HaveCount(2);
            dashboard.AcceptedSeatCount.Should().Be(1);
            dashboard.InvitedSeatCount.Should().Be(2);
            dashboard.SeatLimit.Should().Be(3);
        }

        public void Dispose()
        {
            _workspaceService.Dispose();
            _accountMetadataService.Dispose();
            GlossaryService.DisposeForTests();

            try
            {
                if (Directory.Exists(_basePath))
                {
                    Directory.Delete(_basePath, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
