using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class DemoDatasetServiceTests
    {
        [Fact]
        public void BuildDeterministicReplay_Should_Return_Same_Order_For_Same_Seed()
        {
            var service = new DemoDatasetService();

            var first = service.BuildDeterministicReplay(seed: 1337, stepCount: 5);
            var second = service.BuildDeterministicReplay(seed: 1337, stepCount: 5);

            first.Should().HaveCount(5);
            second.Should().HaveCount(5);
            for (int i = 0; i < first.Count; i++)
            {
                first[i].ClauseId.Should().Be(second[i].ClauseId);
            }
        }
    }
}
