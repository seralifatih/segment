using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class TranslationProviderOrchestrator
    {
        private readonly ITranslationProviderRegistry _registry;
        private readonly ProviderResiliencePolicy _resiliencePolicy;
        private readonly ConcurrentDictionary<string, TranslationProviderHealthSnapshot> _health = new(StringComparer.OrdinalIgnoreCase);

        public TranslationProviderOrchestrator(ITranslationProviderRegistry registry, ProviderResiliencePolicy resiliencePolicy)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _resiliencePolicy = resiliencePolicy ?? throw new ArgumentNullException(nameof(resiliencePolicy));
        }

        public async Task<TranslationProviderResult> ExecuteAsync(
            IReadOnlyList<string> providerChain,
            TranslationProviderRequest request,
            TranslationContext context,
            CancellationToken cancellationToken)
        {
            if (providerChain == null || providerChain.Count == 0)
            {
                return TranslationProviderResult.Fail("No provider chain configured.");
            }

            TranslationProviderResult lastFailure = TranslationProviderResult.Fail("No provider executed.");
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool budgetEnforced = request.IsShortSegmentMode;
            int budgetMs = Math.Max(200, request.RequestBudgetMs);
            int providerIndex = 0;

            foreach (string providerName in providerChain.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                providerIndex++;
                if (!_registry.TryGet(providerName, out ITranslationProvider provider))
                {
                    lastFailure = TranslationProviderResult.Fail($"Provider '{providerName}' is not registered.");
                    lastFailure.BudgetEnforced = budgetEnforced;
                    lastFailure.ProviderRoundtripMs = totalStopwatch.Elapsed.TotalMilliseconds;
                    MarkHealth(providerName, TranslationProviderHealthStatus.Unhealthy, lastFailure.ErrorMessage);
                    continue;
                }

                if (request.RequiresStreaming && !provider.SupportsStreaming)
                {
                    lastFailure = TranslationProviderResult.Fail($"Provider '{provider.Name}' does not support streaming.");
                    lastFailure.BudgetEnforced = budgetEnforced;
                    lastFailure.ProviderRoundtripMs = totalStopwatch.Elapsed.TotalMilliseconds;
                    MarkHealth(provider.Name, TranslationProviderHealthStatus.Degraded, "Streaming not supported.");
                    continue;
                }

                int remainingBudget = budgetMs - (int)totalStopwatch.Elapsed.TotalMilliseconds;
                if (budgetEnforced && remainingBudget <= 0)
                {
                    return new TranslationProviderResult
                    {
                        Success = false,
                        ErrorMessage = "Short-segment latency budget exceeded before provider execution.",
                        BudgetEnforced = true,
                        BudgetExceeded = true,
                        ProviderRoundtripMs = totalStopwatch.Elapsed.TotalMilliseconds
                    };
                }

                var normalizedRequest = NormalizeRequestForProvider(request, provider);
                string route = provider.Name;
                var executionOptions = budgetEnforced
                    ? new ProviderExecutionOptions
                    {
                        MaxRetriesOverride = 0,
                        AttemptTimeoutOverride = TimeSpan.FromMilliseconds(Math.Max(150, remainingBudget))
                    }
                    : null;

                string resultText = await _resiliencePolicy.ExecuteAsync(
                    route,
                    async ct =>
                    {
                        TranslationProviderResult r = await provider.TranslateAsync(normalizedRequest, context, ct);
                        if (r.Success && !string.IsNullOrWhiteSpace(r.OutputText))
                        {
                            return r.OutputText;
                        }

                        return $"ERROR: {r.ErrorMessage}";
                    },
                    cancellationToken,
                    executionOptions);

                if (!string.IsNullOrWhiteSpace(resultText) && !resultText.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    MarkHealth(provider.Name, TranslationProviderHealthStatus.Healthy, "Translation succeeded.");
                    return new TranslationProviderResult
                    {
                        Success = true,
                        OutputText = resultText,
                        ProviderUsed = provider.Name,
                        UsedFallbackProvider = providerIndex > 1,
                        BudgetEnforced = budgetEnforced,
                        BudgetExceeded = false,
                        ProviderRoundtripMs = totalStopwatch.Elapsed.TotalMilliseconds
                    };
                }

                string error = string.IsNullOrWhiteSpace(resultText) ? "Provider returned empty result." : resultText;
                lastFailure = TranslationProviderResult.Fail(error);
                lastFailure.ProviderUsed = provider.Name;
                lastFailure.UsedFallbackProvider = providerIndex > 1;
                lastFailure.BudgetEnforced = budgetEnforced;
                lastFailure.BudgetExceeded = budgetEnforced && totalStopwatch.Elapsed.TotalMilliseconds > budgetMs;
                lastFailure.ProviderRoundtripMs = totalStopwatch.Elapsed.TotalMilliseconds;
                MarkHealth(provider.Name, TranslationProviderHealthStatus.Unhealthy, error);
            }

            return lastFailure;
        }

        public IReadOnlyList<TranslationProviderHealthSnapshot> GetHealthSnapshots()
        {
            var providers = _registry.GetAll();
            return providers
                .Select(x => _health.TryGetValue(x.Name, out var snapshot)
                    ? snapshot
                    : new TranslationProviderHealthSnapshot
                    {
                        ProviderName = x.Name,
                        Status = TranslationProviderHealthStatus.Unknown,
                        CheckedAtUtc = DateTime.UtcNow,
                        Message = "No translation attempts yet."
                    })
                .OrderBy(x => x.ProviderName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<IReadOnlyList<TranslationProviderHealthSnapshot>> RefreshHealthAsync(CancellationToken cancellationToken)
        {
            var snapshots = new List<TranslationProviderHealthSnapshot>();
            foreach (ITranslationProvider provider in _registry.GetAll())
            {
                try
                {
                    TranslationProviderHealthSnapshot health = await provider.HealthCheckAsync(cancellationToken);
                    health.ProviderName = provider.Name;
                    snapshots.Add(health);
                    _health[provider.Name] = health;
                }
                catch (Exception ex)
                {
                    var failed = new TranslationProviderHealthSnapshot
                    {
                        ProviderName = provider.Name,
                        Status = TranslationProviderHealthStatus.Unhealthy,
                        CheckedAtUtc = DateTime.UtcNow,
                        Message = ex.GetType().Name
                    };
                    snapshots.Add(failed);
                    _health[provider.Name] = failed;
                }
            }

            return snapshots
                .OrderBy(x => x.ProviderName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static TranslationProviderRequest NormalizeRequestForProvider(TranslationProviderRequest request, ITranslationProvider provider)
        {
            if (provider.SupportsGlossaryHints)
            {
                return request;
            }

            return new TranslationProviderRequest
            {
                InputText = request.InputText,
                TargetLanguage = request.TargetLanguage,
                PromptPolicy = request.PromptPolicy,
                RequiresStreaming = request.RequiresStreaming,
                IsShortSegmentMode = request.IsShortSegmentMode,
                RequestBudgetMs = request.RequestBudgetMs,
                GlossaryHints = new Dictionary<string, string>()
            };
        }

        private void MarkHealth(string providerName, TranslationProviderHealthStatus status, string message)
        {
            _health[providerName] = new TranslationProviderHealthSnapshot
            {
                ProviderName = providerName,
                Status = status,
                CheckedAtUtc = DateTime.UtcNow,
                Message = message ?? string.Empty
            };
        }
    }
}
