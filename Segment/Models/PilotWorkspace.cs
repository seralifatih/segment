using System;
using System.Collections.Generic;
using LiteDB;

namespace Segment.App.Models
{
    public class PilotWorkspace
    {
        [BsonId]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string AgencyName { get; set; } = "";
        public string OwnerUserId { get; set; } = "";
        public int SeatLimit { get; set; } = 5;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string SharedGlossaryProfileName { get; set; } = "";
        public DateTime? SharedGlossaryBootstrappedAtUtc { get; set; }
        public int SharedGlossaryTermCount { get; set; }
        public List<PilotSeatInvite> SeatInvites { get; set; } = new();
        public List<PilotWorkspaceKpiSample> KpiSamples { get; set; } = new();
        public List<string> PartnerTags { get; set; } = new();
    }
}
