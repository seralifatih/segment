using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IPilotWorkspaceService
    {
        PilotWorkspace CreateWorkspace(string agencyName, string ownerUserId, int seatLimit, IEnumerable<string>? partnerTags = null);
        PilotWorkspace GetWorkspace(string workspaceId);
        PilotSeatInvite InviteSeat(string workspaceId, string email);
        void AcceptSeatInvite(string workspaceId, string email);
        int BootstrapSharedGlossary(string workspaceId, IReadOnlyList<TermEntry>? seedTerms = null);
        void RecordKpiSample(string workspaceId, PilotWorkspaceKpiSample sample);
        PilotWorkspaceKpiDashboard GetKpiDashboard(string workspaceId);
    }
}
