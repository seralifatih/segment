using System;
using System.IO;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    [Collection("Database Tests")]
    public class PilotWorkspaceServiceTests : IDisposable
    {
        private readonly string _basePath;
        private readonly PilotWorkspaceService _service;

        public PilotWorkspaceServiceTests()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "SegmentPilotWorkspaceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_basePath);
            _service = new PilotWorkspaceService(_basePath);
        }

        [Fact]
        public void GetKpiDashboard_Should_Aggregate_Kpis_And_Seat_Utilization()
        {
            var workspace = _service.CreateWorkspace("Agency Legal", "owner-1", seatLimit: 5, partnerTags: new[] { "association", "bar-council" });
            _service.InviteSeat(workspace.Id, "a@agency.com");
            _service.InviteSeat(workspace.Id, "b@agency.com");
            _service.AcceptSeatInvite(workspace.Id, "a@agency.com");

            _service.RecordKpiSample(workspace.Id, new PilotWorkspaceKpiSample
            {
                Retention30d = 0.72,
                TrialToPaidConversion = 0.22,
                P95LatencyMs = 1200,
                TermViolationRate = 0.028,
                ActiveSeatCount = 2
            });

            _service.RecordKpiSample(workspace.Id, new PilotWorkspaceKpiSample
            {
                Retention30d = 0.78,
                TrialToPaidConversion = 0.30,
                P95LatencyMs = 980,
                TermViolationRate = 0.018,
                ActiveSeatCount = 3
            });

            var dashboard = _service.GetKpiDashboard(workspace.Id);

            dashboard.WorkspaceId.Should().Be(workspace.Id);
            dashboard.InvitedSeatCount.Should().Be(2);
            dashboard.AcceptedSeatCount.Should().Be(1);
            dashboard.SeatUtilizationRate.Should().BeApproximately(0.2, 0.0001);
            dashboard.AverageRetention30d.Should().BeApproximately(0.75, 0.0001);
            dashboard.AverageTrialToPaidConversion.Should().BeApproximately(0.26, 0.0001);
            dashboard.AverageP95LatencyMs.Should().BeApproximately(1090, 0.01);
            dashboard.AverageTermViolationRate.Should().BeApproximately(0.023, 0.0001);
            dashboard.TotalSamples.Should().Be(2);
        }

        public void Dispose()
        {
            _service.Dispose();
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
