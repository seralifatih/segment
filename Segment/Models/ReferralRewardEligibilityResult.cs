namespace Segment.App.Models
{
    public class ReferralRewardEligibilityResult
    {
        public bool Eligible { get; set; }
        public bool RewardGranted { get; set; }
        public bool AlreadyRewarded { get; set; }
        public string Reason { get; set; } = "";
        public string? ReferrerUserId { get; set; }
        public string? ReferredUserId { get; set; }
    }
}
