using System;

namespace Segment.App.Models
{
    public class PilotSeatInvite
    {
        public string Email { get; set; } = "";
        public DateTime InvitedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAtUtc { get; set; }
        public PilotSeatInviteStatus Status { get; set; } = PilotSeatInviteStatus.Pending;
    }
}
