using System.Collections.Generic;
using System.Linq;

namespace Segment.App.Models
{
    public class PastebackDecision
    {
        public GuardrailValidationResult Validation { get; set; } = new();
        public bool AutoPasteAllowed => !Validation.Results.Any(x => x.IsBlocking);
        public IReadOnlyList<GuardrailResult> BlockingIssues => Validation.Results.Where(x => x.IsBlocking).ToList();
    }
}
