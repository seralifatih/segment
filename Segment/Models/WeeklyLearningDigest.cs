using System;
using System.Collections.Generic;

namespace Segment.App.Models
{
    public class WeeklyLearningDigest
    {
        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public int TermsLearned { get; set; }
        public int UnresolvedConflicts { get; set; }
        public IReadOnlyList<string> LearnedTerms { get; set; } = Array.Empty<string>();
    }
}
