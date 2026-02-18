using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Segment.App.Services
{
    public class ProviderResiliencePolicy
    {
        private sealed class CircuitState
        {
            public int ConsecutiveFailures { get; set; }
            public DateTime? OpenUntilUtc { get; set; }
        }

        private readonly ConcurrentDictionary<string, CircuitState> _states = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<DateTime> _utcNow;

        public int FailureThreshold { get; }
        public TimeSpan CircuitOpenDuration { get; }
        public int MaxRetries { get; }
        public TimeSpan AttemptTimeout { get; }

        public ProviderResiliencePolicy(
            int failureThreshold = 3,
            int maxRetries = 2,
            int attemptTimeoutMs = 15000,
            int circuitOpenDurationMs = 30000,
            Func<DateTime>? utcNowProvider = null)
        {
            FailureThreshold = Math.Max(1, failureThreshold);
            MaxRetries = Math.Max(0, maxRetries);
            AttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1000, attemptTimeoutMs));
            CircuitOpenDuration = TimeSpan.FromMilliseconds(Math.Max(1000, circuitOpenDurationMs));
            _utcNow = utcNowProvider ?? (() => DateTime.UtcNow);
        }

        public bool IsCircuitOpen(string route)
        {
            string key = NormalizeRoute(route);
            CircuitState state = _states.GetOrAdd(key, _ => new CircuitState());
            if (state.OpenUntilUtc == null)
            {
                return false;
            }

            if (_utcNow() >= state.OpenUntilUtc.Value)
            {
                state.OpenUntilUtc = null;
                state.ConsecutiveFailures = 0;
                return false;
            }

            return true;
        }

        public async Task<string> ExecuteAsync(string route, Func<CancellationToken, Task<string>> attempt, CancellationToken cancellationToken, ProviderExecutionOptions? options = null)
        {
            if (IsCircuitOpen(route))
            {
                return "ERROR: Provider circuit is open due to recent failures. Please retry shortly.";
            }

            int effectiveRetries = options?.MaxRetriesOverride ?? MaxRetries;
            if (effectiveRetries < 0) effectiveRetries = 0;
            TimeSpan effectiveTimeout = options?.AttemptTimeoutOverride ?? AttemptTimeout;
            if (effectiveTimeout < TimeSpan.FromMilliseconds(100))
            {
                effectiveTimeout = TimeSpan.FromMilliseconds(100);
            }

            Exception? lastException = null;
            for (int i = 0; i <= effectiveRetries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(effectiveTimeout);

                try
                {
                    string result = await attempt(cts.Token);
                    if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        RecordSuccess(route);
                        return result;
                    }

                    lastException = new InvalidOperationException(result);
                    RecordFailure(route);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
                {
                    lastException = ex;
                    RecordFailure(route);
                }

                if (i < effectiveRetries)
                {
                    await Task.Delay(100 * (i + 1), cancellationToken);
                }
            }

            if (lastException is OperationCanceledException)
            {
                return "ERROR: Provider request timed out.";
            }

            return lastException == null
                ? "ERROR: Provider request failed."
                : $"ERROR: {lastException.Message}";
        }

        public void RecordSuccess(string route)
        {
            CircuitState state = _states.GetOrAdd(NormalizeRoute(route), _ => new CircuitState());
            state.ConsecutiveFailures = 0;
            state.OpenUntilUtc = null;
        }

        public void RecordFailure(string route)
        {
            CircuitState state = _states.GetOrAdd(NormalizeRoute(route), _ => new CircuitState());
            state.ConsecutiveFailures++;
            if (state.ConsecutiveFailures >= FailureThreshold)
            {
                state.OpenUntilUtc = _utcNow().Add(CircuitOpenDuration);
            }
        }

        private static string NormalizeRoute(string route)
        {
            return string.IsNullOrWhiteSpace(route) ? "default" : route.Trim();
        }
    }
}
