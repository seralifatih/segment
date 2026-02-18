using System.Collections.Generic;
using System.Linq;

namespace Segment.App.Models
{
    public class GuardrailValidationResult
    {
        public IReadOnlyList<GuardrailResult> Results { get; set; } = new List<GuardrailResult>();
        public bool HasBlockingIssues => Results.Any(x => x.IsBlocking);
    }
}
