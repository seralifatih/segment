using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class TranslationProviderOrchestratorTests
    {
        [Fact]
        public async Task ExecuteAsync_Should_Fallback_From_Primary_To_Secondary()
        {
            var registry = new TranslationProviderRegistry();
            registry.Register(new FakeProvider("Primary", supportsStreaming: false, supportsGlossaryHints: true,
                translate: _ => Task.FromResult(TranslationProviderResult.Fail("primary failed"))));
            registry.Register(new FakeProvider("Secondary", supportsStreaming: false, supportsGlossaryHints: true,
                translate: _ => Task.FromResult(TranslationProviderResult.Ok("secondary output"))));

            var orchestrator = new TranslationProviderOrchestrator(
                registry,
                new ProviderResiliencePolicy(failureThreshold: 2, maxRetries: 0, attemptTimeoutMs: 2000, circuitOpenDurationMs: 1000));

            TranslationProviderResult result = await orchestrator.ExecuteAsync(
                new[] { "Primary", "Secondary" },
                new TranslationProviderRequest
                {
                    InputText = "hello",
                    TargetLanguage = "Turkish",
                    PromptPolicy = "policy"
                },
                new TranslationContext(),
                CancellationToken.None);

            result.Success.Should().BeTrue();
            result.OutputText.Should().Be("secondary output");
        }

        [Fact]
        public async Task ExecuteAsync_Should_Transition_Health_Unhealthy_To_Healthy()
        {
            var outcomes = new Queue<TranslationProviderResult>(new[]
            {
                TranslationProviderResult.Fail("boom"),
                TranslationProviderResult.Ok("ok")
            });
            var provider = new FakeProvider("Primary", supportsStreaming: false, supportsGlossaryHints: true,
                translate: _ => Task.FromResult(outcomes.Dequeue()));

            var registry = new TranslationProviderRegistry();
            registry.Register(provider);

            var orchestrator = new TranslationProviderOrchestrator(
                registry,
                new ProviderResiliencePolicy(failureThreshold: 2, maxRetries: 0, attemptTimeoutMs: 2000, circuitOpenDurationMs: 1000));

            TranslationProviderResult first = await orchestrator.ExecuteAsync(
                new[] { "Primary" },
                new TranslationProviderRequest { InputText = "x", TargetLanguage = "tr", PromptPolicy = "p" },
                new TranslationContext(),
                CancellationToken.None);

            first.Success.Should().BeFalse();
            orchestrator.GetHealthSnapshots().Should().Contain(x => x.ProviderName == "Primary" && x.Status == TranslationProviderHealthStatus.Unhealthy);

            TranslationProviderResult second = await orchestrator.ExecuteAsync(
                new[] { "Primary" },
                new TranslationProviderRequest { InputText = "x", TargetLanguage = "tr", PromptPolicy = "p" },
                new TranslationContext(),
                CancellationToken.None);

            second.Success.Should().BeTrue();
            orchestrator.GetHealthSnapshots().Should().Contain(x => x.ProviderName == "Primary" && x.Status == TranslationProviderHealthStatus.Healthy);
        }

        [Fact]
        public async Task ExecuteAsync_Should_Skip_Streaming_Unsupported_Provider()
        {
            bool secondaryCalled = false;
            var registry = new TranslationProviderRegistry();
            registry.Register(new FakeProvider("Primary", supportsStreaming: false, supportsGlossaryHints: true,
                translate: _ => Task.FromResult(TranslationProviderResult.Ok("should-not-run"))));
            registry.Register(new FakeProvider("Secondary", supportsStreaming: true, supportsGlossaryHints: true,
                translate: _ =>
                {
                    secondaryCalled = true;
                    return Task.FromResult(TranslationProviderResult.Ok("streaming-ok"));
                }));

            var orchestrator = new TranslationProviderOrchestrator(
                registry,
                new ProviderResiliencePolicy(failureThreshold: 1, maxRetries: 0, attemptTimeoutMs: 2000, circuitOpenDurationMs: 1000));

            TranslationProviderResult result = await orchestrator.ExecuteAsync(
                new[] { "Primary", "Secondary" },
                new TranslationProviderRequest
                {
                    InputText = "hello",
                    TargetLanguage = "Turkish",
                    PromptPolicy = "policy",
                    RequiresStreaming = true
                },
                new TranslationContext(),
                CancellationToken.None);

            result.Success.Should().BeTrue();
            result.OutputText.Should().Be("streaming-ok");
            secondaryCalled.Should().BeTrue();
        }

        [Fact]
        public async Task ExecuteAsync_Should_Not_Pass_GlossaryHints_When_Provider_Does_Not_Support_Them()
        {
            int hintCountSeen = -1;
            var registry = new TranslationProviderRegistry();
            registry.Register(new FakeProvider("Primary", supportsStreaming: false, supportsGlossaryHints: false,
                translate: req =>
                {
                    hintCountSeen = req.GlossaryHints.Count;
                    return Task.FromResult(TranslationProviderResult.Ok("ok"));
                }));

            var orchestrator = new TranslationProviderOrchestrator(
                registry,
                new ProviderResiliencePolicy(failureThreshold: 1, maxRetries: 0, attemptTimeoutMs: 2000, circuitOpenDurationMs: 1000));

            TranslationProviderResult result = await orchestrator.ExecuteAsync(
                new[] { "Primary" },
                new TranslationProviderRequest
                {
                    InputText = "hello",
                    TargetLanguage = "Turkish",
                    PromptPolicy = "policy",
                    GlossaryHints = new Dictionary<string, string>
                    {
                        ["agreement"] = "sozlesme"
                    }
                },
                new TranslationContext(),
                CancellationToken.None);

            result.Success.Should().BeTrue();
            hintCountSeen.Should().Be(0);
        }

        [Fact]
        public async Task ExecuteAsync_ShortSegmentMode_Should_Disable_Retries()
        {
            int attempts = 0;
            var registry = new TranslationProviderRegistry();
            registry.Register(new FakeProvider("Primary", supportsStreaming: false, supportsGlossaryHints: true,
                translate: _ =>
                {
                    attempts++;
                    return Task.FromResult(TranslationProviderResult.Fail("nope"));
                }));

            var orchestrator = new TranslationProviderOrchestrator(
                registry,
                new ProviderResiliencePolicy(failureThreshold: 5, maxRetries: 2, attemptTimeoutMs: 2000, circuitOpenDurationMs: 1000));

            TranslationProviderResult result = await orchestrator.ExecuteAsync(
                new[] { "Primary" },
                new TranslationProviderRequest
                {
                    InputText = "short",
                    TargetLanguage = "Turkish",
                    PromptPolicy = "policy",
                    IsShortSegmentMode = true,
                    RequestBudgetMs = 700
                },
                new TranslationContext(),
                CancellationToken.None);

            result.Success.Should().BeFalse();
            attempts.Should().Be(1);
        }

        [Fact]
        public async Task ExecuteAsync_ShortSegmentMode_Should_Return_BudgetExceeded()
        {
            var registry = new TranslationProviderRegistry();
            registry.Register(new FakeProvider("Primary", supportsStreaming: false, supportsGlossaryHints: true,
                translate: async (_, ct) =>
                {
                    await Task.Delay(300, ct);
                    return TranslationProviderResult.Ok("late");
                }));

            var orchestrator = new TranslationProviderOrchestrator(
                registry,
                new ProviderResiliencePolicy(failureThreshold: 3, maxRetries: 0, attemptTimeoutMs: 5000, circuitOpenDurationMs: 1000));

            TranslationProviderResult result = await orchestrator.ExecuteAsync(
                new[] { "Primary" },
                new TranslationProviderRequest
                {
                    InputText = "short",
                    TargetLanguage = "Turkish",
                    PromptPolicy = "policy",
                    IsShortSegmentMode = true,
                    RequestBudgetMs = 150
                },
                new TranslationContext(),
                CancellationToken.None);

            result.Success.Should().BeFalse();
            result.BudgetEnforced.Should().BeTrue();
            result.BudgetExceeded.Should().BeTrue();
        }

        private sealed class FakeProvider : ITranslationProvider
        {
            private readonly Func<TranslationProviderRequest, CancellationToken, Task<TranslationProviderResult>> _translate;

            public FakeProvider(string name, bool supportsStreaming, bool supportsGlossaryHints, Func<TranslationProviderRequest, Task<TranslationProviderResult>> translate)
                : this(name, supportsStreaming, supportsGlossaryHints, (request, _) => translate(request))
            {
            }

            public FakeProvider(string name, bool supportsStreaming, bool supportsGlossaryHints, Func<TranslationProviderRequest, CancellationToken, Task<TranslationProviderResult>> translate)
            {
                Name = name;
                SupportsStreaming = supportsStreaming;
                SupportsGlossaryHints = supportsGlossaryHints;
                _translate = translate;
            }

            public string Name { get; }
            public bool SupportsStreaming { get; }
            public bool SupportsGlossaryHints { get; }

            public Task<TranslationProviderResult> TranslateAsync(TranslationProviderRequest request, TranslationContext context, CancellationToken cancellationToken)
            {
                return _translate(request, cancellationToken);
            }

            public Task<TranslationProviderHealthSnapshot> HealthCheckAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new TranslationProviderHealthSnapshot
                {
                    ProviderName = Name,
                    Status = TranslationProviderHealthStatus.Healthy,
                    CheckedAtUtc = DateTime.UtcNow,
                    Message = "ok"
                });
            }
        }
    }
}
