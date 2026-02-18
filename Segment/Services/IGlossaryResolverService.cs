using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IGlossaryResolverService
    {
        TermResolutionResult ResolveTerm(string sourceTerm, TermResolutionContext context);
    }
}
