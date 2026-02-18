using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IRoiBenchmarkCalculator
    {
        PilotRoiReport Calculate(BenchmarkSession session);
    }
}
