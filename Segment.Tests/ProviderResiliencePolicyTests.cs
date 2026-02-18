using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class ProviderResiliencePolicyTests
    {
        [Fact]
        public async Task ExecuteAsync_Should_Retry_Then_Succeed()
        {
            var policy = new ProviderResiliencePolicy(failureThreshold: 3, maxRetries: 2, attemptTimeoutMs: 5000, circuitOpenDurationMs: 1000);
            int attempts = 0;

            string result = await policy.ExecuteAsync(
                "Google",
                _ =>
                {
                    attempts++;
                    return Task.FromResult(attempts < 3 ? "ERROR: transient" : "ok");
                },
                CancellationToken.None);

            result.Should().Be("ok");
            attempts.Should().Be(3);
        }

        [Fact]
        public async Task ExecuteAsync_Should_Open_Circuit_After_Threshold()
        {
            DateTime now = DateTime.UtcNow;
            var policy = new ProviderResiliencePolicy(
                failureThreshold: 2,
                maxRetries: 0,
                attemptTimeoutMs: 5000,
                circuitOpenDurationMs: 30000,
                utcNowProvider: () => now);

            await policy.ExecuteAsync("Custom", _ => Task.FromResult("ERROR: fail"), CancellationToken.None);
            await policy.ExecuteAsync("Custom", _ => Task.FromResult("ERROR: fail"), CancellationToken.None);

            policy.IsCircuitOpen("Custom").Should().BeTrue();
            string blocked = await policy.ExecuteAsync("Custom", _ => Task.FromResult("ok"), CancellationToken.None);
            blocked.Should().Contain("circuit is open");
        }

        [Fact]
        public async Task ExecuteAsync_Should_Use_ExecutionOptions_For_Timeout_And_Retry()
        {
            var policy = new ProviderResiliencePolicy(failureThreshold: 3, maxRetries: 3, attemptTimeoutMs: 5000, circuitOpenDurationMs: 1000);
            int attempts = 0;

            string result = await policy.ExecuteAsync(
                "Google",
                async ct =>
                {
                    attempts++;
                    await Task.Delay(250, ct);
                    return "ok";
                },
                CancellationToken.None,
                new ProviderExecutionOptions
                {
                    MaxRetriesOverride = 0,
                    AttemptTimeoutOverride = TimeSpan.FromMilliseconds(100)
                });

            result.Should().Contain("timed out");
            attempts.Should().Be(1);
        }
    }
}
