using System.Collections.Generic;

namespace Segment.App.Models
{
    public class TermResolutionResult
    {
        public TermEntry? Winner { get; set; }
        public string Reason { get; set; } = "";
        public IReadOnlyList<TermEntry> Candidates { get; set; } = new List<TermEntry>();
        public string WinningRule { get; set; } = "";
        public string ScopePrecedenceApplied { get; set; } = "Project > Team > User > System";
        public bool IsLowConfidenceCollision { get; set; }
        public bool RequiresUserSelection { get; set; }
        public IReadOnlyList<string> DecisionTrace { get; set; } = new List<string>();
    }
}
