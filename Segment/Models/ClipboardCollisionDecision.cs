namespace Segment.App.Models
{
    public class ClipboardCollisionDecision
    {
        public bool AllowOverwrite { get; set; }
        public string Reason { get; set; } = "";
    }
}
