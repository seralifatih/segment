namespace Segment.App.Models
{
    public class LaunchUserContext
    {
        public bool IsAdminUser { get; set; }
        public bool IsInvitedUser { get; set; }
        public bool IsLegalNicheUser { get; set; } = true;
        public bool IsAgencyAccount { get; set; }
        public bool HasPilotContract { get; set; }
    }
}
