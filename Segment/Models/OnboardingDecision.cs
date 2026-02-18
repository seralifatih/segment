namespace Segment.App.Models
{
    public class OnboardingDecision
    {
        public OnboardingOutcome Outcome { get; set; } = OnboardingOutcome.Waitlist;
        public int Score { get; set; }
        public string Explanation { get; set; } = "";
    }
}
