using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface IDemoDatasetService
    {
        DemoDatasetPackage GetLegalDemoDataset();
        IReadOnlyList<DemoReplayFrame> BuildDeterministicReplay(int seed, int stepCount);
    }
}
