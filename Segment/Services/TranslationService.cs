using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Segment.App.Models;

namespace Segment.App.Services
{
    public static class TranslationService
    {
        private const int ShortSegmentCharThreshold = 220;
        private const int ShortSegmentBudgetMs = 700;
        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static ITextRedactionService _redactionService = new TextRedactionService();
        private static readonly PromptPolicyComposer _promptPolicyComposer = new PromptPolicyComposer(new DomainProfileService());
        private static readonly INicheTemplateService _nicheTemplateService = new NicheTemplateService();
        private static readonly TranslationResultCacheService _cache = new TranslationResultCacheService();
        private static readonly ProviderResiliencePolicy _resiliencePolicy = new ProviderResiliencePolicy();
        private static readonly StructuredLogger _logger = new StructuredLogger();
        private static readonly ITranslationProviderRegistry _providerRegistry = BuildProviderRegistry();
        private static readonly TranslationProviderOrchestrator _providerOrchestrator = new TranslationProviderOrchestrator(
            _providerRegistry,
            _resiliencePolicy);

        public static void SetRedactionService(ITextRedactionService service)
        {
            _redactionService = service ?? new TextRedactionService();
        }

        public static async Task<string> SuggestAsync(string input)
        {
            return await SuggestAsync(input, CancellationToken.None);
        }

        public static async Task<string> SuggestAsync(string input, CancellationToken cancellationToken)
        {
            TranslationExecutionResult execution = await SuggestWithMetricsAsync(input, cancellationToken);
            return execution.OutputText;
        }

        public static async Task<TranslationExecutionResult> SuggestWithMetricsAsync(string input)
        {
            return await SuggestWithMetricsAsync(input, CancellationToken.None);
        }

        public static async Task<TranslationExecutionResult> SuggestWithMetricsAsync(string input, CancellationToken cancellationToken)
        {
            var settings = SettingsService.Current;
            string safeInput = PromptSafetySanitizer.SanitizeUntrustedSourceText(input ?? string.Empty);
            string targetLang = settings.TargetLanguage;
            ProviderRoutingDecision route = ResolveRoutingDecision(settings);
            DomainVertical effectiveDomain = ResolveEffectiveDomain();
            bool isShortSegmentMode = IsShortSegment(safeInput);
            string glossaryVersion = GlossaryService.GetGlossaryVersionToken();
            string cacheKey = TranslationResultCacheService.BuildKey(
                safeInput,
                sourceLanguage: "English",
                targetLanguage: targetLang,
                glossaryVersion: glossaryVersion,
                domain: effectiveDomain.ToString());

            if (_cache.TryGet(cacheKey, out string cached))
            {
                _logger.Info("translation_cache_hit", new Dictionary<string, string>
                {
                    ["route"] = route.EffectiveRoute,
                    ["domain"] = effectiveDomain.ToString(),
                    ["glossary_version"] = glossaryVersion
                });
                return new TranslationExecutionResult
                {
                    OutputText = cached,
                    Success = true,
                    ProviderUsed = "cache",
                    ProviderRoundtripMs = 0,
                    IsShortSegmentMode = isShortSegmentMode
                };
            }

            ComplianceAuditService.Default.Record(new ComplianceAuditRecord
            {
                EventType = ComplianceAuditEventType.RoutingDecision,
                AccountId = settings.AccountId,
                Decision = route.IsBlocked ? "blocked" : "allowed",
                ActiveMode = route.ConfidentialityMode.ToString(),
                ProviderRoute = route.EffectiveRoute,
                RetentionPolicySummary = settings.RetentionPolicySummary,
                Details = route.Reason,
                Metadata = new Dictionary<string, string>
                {
                    ["requested_provider"] = route.RequestedProvider,
                    ["effective_route"] = route.EffectiveRoute,
                    ["blocked"] = route.IsBlocked.ToString(),
                    ["confidentiality_mode"] = route.ConfidentialityMode.ToString(),
                    ["redaction"] = route.ApplyRedactionBeforeCloudCall.ToString()
                }
            });

            if (route.IsBlocked)
            {
                _logger.Info("translation_route_blocked", new Dictionary<string, string>
                {
                    ["route"] = route.EffectiveRoute,
                    ["domain"] = effectiveDomain.ToString(),
                    ["reason"] = route.Reason
                });
                return new TranslationExecutionResult
                {
                    OutputText = $"ERROR: {route.Reason}",
                    Success = false,
                    ProviderUsed = route.EffectiveRoute,
                    IsShortSegmentMode = isShortSegmentMode
                };
            }

            string redactionInfo = "none";
            if (route.ApplyRedactionBeforeCloudCall)
            {
                RedactionResult redactedInput = _redactionService.Redact(safeInput);
                safeInput = redactedInput.RedactedText;
                redactionInfo = redactedInput.TokenToOriginalMap.Count == 0
                    ? "redacted_no_tokens"
                    : $"redacted_tokens:{redactedInput.TokenToOriginalMap.Count}";
            }

            IReadOnlyDictionary<string, string> glossaryHints = ResolveLockedTerminology(safeInput, effectiveDomain);
            glossaryHints = PromptSafetySanitizer.SanitizeGlossaryConstraints(glossaryHints);
            string promptPolicy = BuildPromptPolicy(input ?? string.Empty);

            ComplianceAuditService.Default.Record(new ComplianceAuditRecord
            {
                EventType = ComplianceAuditEventType.RoutingDecision,
                AccountId = settings.AccountId,
                Decision = "request_processed",
                ActiveMode = route.ConfidentialityMode.ToString(),
                ProviderRoute = route.EffectiveRoute,
                RetentionPolicySummary = settings.RetentionPolicySummary,
                Details = "Translation request prepared for provider route.",
                Metadata = new Dictionary<string, string>
                {
                    ["redaction_info"] = redactionInfo
                }
            });

            var providerRequest = new TranslationProviderRequest
            {
                InputText = safeInput,
                TargetLanguage = targetLang,
                PromptPolicy = promptPolicy,
                GlossaryHints = glossaryHints,
                RequiresStreaming = false,
                IsShortSegmentMode = isShortSegmentMode,
                RequestBudgetMs = ShortSegmentBudgetMs
            };

            var providerContext = new TranslationContext
            {
                Domain = effectiveDomain,
                SourceLanguage = "English",
                TargetLanguage = targetLang,
                AccountId = settings.AccountId,
                LockedTerminology = glossaryHints
            };

            IReadOnlyList<string> providerChain = BuildProviderChain(route, settings);
            TranslationProviderResult providerResult = await _providerOrchestrator.ExecuteAsync(
                providerChain,
                providerRequest,
                providerContext,
                cancellationToken);

            string result = providerResult.Success
                ? providerResult.OutputText
                : $"ERROR: {providerResult.ErrorMessage}";

            _logger.Info("translation_completed", new Dictionary<string, string>
            {
                ["route"] = route.EffectiveRoute,
                ["domain"] = effectiveDomain.ToString(),
                ["success"] = (!result.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)).ToString(),
                ["glossary_version"] = glossaryVersion
            });

            if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                _cache.Set(cacheKey, result);
            }

            return new TranslationExecutionResult
            {
                OutputText = result,
                Success = providerResult.Success,
                ProviderUsed = providerResult.ProviderUsed,
                UsedFallbackProvider = providerResult.UsedFallbackProvider,
                BudgetEnforced = providerResult.BudgetEnforced,
                BudgetExceeded = providerResult.BudgetExceeded,
                ProviderRoundtripMs = providerResult.ProviderRoundtripMs,
                IsShortSegmentMode = isShortSegmentMode
            };
        }

        public static async Task WarmupSelectedProviderAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var settings = SettingsService.Current;
                ProviderRoutingDecision route = ResolveRoutingDecision(settings);
                if (route.IsBlocked)
                {
                    return;
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(4));
                if (_providerRegistry.TryGet(route.EffectiveRoute, out ITranslationProvider provider))
                {
                    TranslationProviderHealthSnapshot snapshot = await provider.HealthCheckAsync(cts.Token);
                    _logger.Info("provider_warmup", new Dictionary<string, string>
                    {
                        ["route"] = provider.Name,
                        ["status"] = snapshot.Status.ToString(),
                        ["message"] = snapshot.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error("provider_warmup_failed", ex);
            }
        }

        public static ProviderRoutingDecision ResolveRoutingDecision(AppConfig settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            string requested = string.IsNullOrWhiteSpace(settings.AiProvider) ? "Google" : settings.AiProvider.Trim();
            bool requestedCloud = string.Equals(requested, "Google", StringComparison.OrdinalIgnoreCase)
                || string.Equals(requested, "Custom", StringComparison.OrdinalIgnoreCase);
            ConfidentialityMode mode = ResolveConfidentialityMode(settings);

            if (mode == ConfidentialityMode.LocalOnly && requestedCloud)
            {
                return new ProviderRoutingDecision
                {
                    RequestedProvider = requested,
                    EffectiveRoute = "LocalOnlyBlockedCloudRoute",
                    ConfidentialityMode = mode,
                    IsLocalOnly = true,
                    IsBlocked = true,
                    ApplyRedactionBeforeCloudCall = false,
                    Reason = "Confidential project mode is enabled. Cloud routing is blocked; switch provider to Ollama."
                };
            }

            string route = NormalizeProviderRoute(requested);
            if (settings.PreferLocalProcessingPath && !string.Equals(route, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                route = "Ollama";
            }

            if (settings.EnforceApprovedProviders && !IsProviderApproved(route, settings))
            {
                return new ProviderRoutingDecision
                {
                    RequestedProvider = requested,
                    EffectiveRoute = route,
                    ConfidentialityMode = mode,
                    IsLocalOnly = mode == ConfidentialityMode.LocalOnly,
                    IsBlocked = true,
                    ApplyRedactionBeforeCloudCall = false,
                    Reason = $"Provider route '{route}' is not approved by policy."
                };
            }

            bool isLocal = mode == ConfidentialityMode.LocalOnly || string.Equals(route, "Ollama", StringComparison.OrdinalIgnoreCase);
            bool applyRedaction = mode == ConfidentialityMode.RedactedCloud && !string.Equals(route, "Ollama", StringComparison.OrdinalIgnoreCase);

            return new ProviderRoutingDecision
            {
                RequestedProvider = requested,
                EffectiveRoute = route,
                ConfidentialityMode = mode,
                ApplyRedactionBeforeCloudCall = applyRedaction,
                IsLocalOnly = isLocal,
                IsBlocked = false,
                Reason = mode switch
                {
                    ConfidentialityMode.LocalOnly => "Confidential local-only mode active.",
                    ConfidentialityMode.RedactedCloud when applyRedaction => "Redacted cloud mode active. Sensitive entities are redacted before cloud calls.",
                    ConfidentialityMode.RedactedCloud => "Redacted cloud mode selected with local route.",
                    _ => "Routing allowed by current provider settings."
                }
            };
        }

        public static DataHandlingDisclosure BuildDataHandlingDisclosure(AppConfig settings)
        {
            ProviderRoutingDecision route = ResolveRoutingDecision(settings);
            return new DataHandlingDisclosure
            {
                ActiveMode = route.ConfidentialityMode.ToString(),
                ProviderRoute = route.IsBlocked
                    ? $"{route.RequestedProvider} (blocked) -> local-only enforcement"
                    : (route.ApplyRedactionBeforeCloudCall
                        ? $"{route.EffectiveRoute} (redacted)"
                        : route.EffectiveRoute),
                RetentionPolicySummary = settings.RetentionPolicySummary
            };
        }

        private static DomainVertical ResolveEffectiveDomain()
        {
            DomainVertical domain = Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical parsedDomain)
                ? parsedDomain
                : DomainVertical.Legal;

            if (_nicheTemplateService.TryGetProjectConfiguration(GlossaryService.CurrentProfile.Name, out ProjectNicheConfiguration config))
            {
                return config.Domain;
            }

            return domain;
        }

        private static ConfidentialityMode ResolveConfidentialityMode(AppConfig settings)
        {
            if (settings.ConfidentialProjectLocalOnly)
            {
                return ConfidentialityMode.LocalOnly;
            }

            return Enum.TryParse(settings.ConfidentialityMode, ignoreCase: true, out ConfidentialityMode parsed)
                ? parsed
                : ConfidentialityMode.Standard;
        }

        private static string NormalizeProviderRoute(string requested)
        {
            if (string.Equals(requested, "Ollama", StringComparison.OrdinalIgnoreCase)) return "Ollama";
            if (string.Equals(requested, "Custom", StringComparison.OrdinalIgnoreCase)) return "Custom";
            return "Google";
        }

        public static async Task<IReadOnlyList<TranslationProviderHealthSnapshot>> GetProviderHealthStatusAsync(CancellationToken cancellationToken = default)
        {
            return await _providerOrchestrator.RefreshHealthAsync(cancellationToken);
        }

        private static IReadOnlyList<string> BuildProviderChain(ProviderRoutingDecision route, AppConfig settings)
        {
            var chain = new List<string>();
            if (!string.IsNullOrWhiteSpace(route.EffectiveRoute))
            {
                chain.Add(route.EffectiveRoute);
            }

            string secondary = NormalizeProviderRoute(settings.SecondaryAiProvider ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(settings.SecondaryAiProvider)
                && !route.IsBlocked
                && !string.Equals(secondary, route.EffectiveRoute, StringComparison.OrdinalIgnoreCase))
            {
                if (route.ConfidentialityMode != ConfidentialityMode.LocalOnly
                    || string.Equals(secondary, "Ollama", StringComparison.OrdinalIgnoreCase))
                {
                    if (!settings.EnforceApprovedProviders || IsProviderApproved(secondary, settings))
                    {
                        chain.Add(secondary);
                    }
                }
            }

            return chain;
        }

        private static ITranslationProviderRegistry BuildProviderRegistry()
        {
            var registry = new TranslationProviderRegistry();
            registry.Register(new GoogleTranslationProvider(httpClient));
            registry.Register(new OllamaTranslationProvider(httpClient));
            registry.Register(new CustomTranslationProvider(httpClient));
            return registry;
        }

        private static bool IsShortSegment(string input)
        {
            return !string.IsNullOrWhiteSpace(input) && input.Trim().Length <= ShortSegmentCharThreshold;
        }

        private static bool IsProviderApproved(string providerRoute, AppConfig settings)
        {
            IReadOnlySet<string> approved = SettingsService.GetApprovedProviders();
            return approved.Count == 0 || approved.Contains(providerRoute);
        }

        private static string BuildPromptPolicy(string input)
        {
            DomainVertical domain = Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical parsedDomain)
                ? parsedDomain
                : DomainVertical.Legal;

            var context = new TranslationContext
            {
                Domain = domain,
                SourceLanguage = "English",
                TargetLanguage = SettingsService.Current.TargetLanguage,
                AccountId = SettingsService.Current.AccountId,
                LockedTerminology = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            if (_nicheTemplateService.TryGetProjectConfiguration(GlossaryService.CurrentProfile.Name, out ProjectNicheConfiguration config))
            {
                context.Domain = config.Domain;
                context.ActiveStyleHints = config.StyleHints;
                context.EnabledQaChecks = config.EnabledQaChecks;
            }

            context.LockedTerminology = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return _promptPolicyComposer.Compose(context);
        }

        private static IReadOnlyDictionary<string, string> ResolveLockedTerminology(string input, DomainVertical domain)
        {
            var resolver = new GlossaryResolverService();
            var allTerms = GlossaryService.GetAllTermsForResolution();
            if (allTerms.Count == 0) return new Dictionary<string, string>();

            var context = new TermResolutionContext
            {
                DomainVertical = domain,
                SourceLanguage = "English",
                TargetLanguage = SettingsService.Current.TargetLanguage,
                UserId = SettingsService.Current.AccountId,
                ProjectId = GlossaryService.CurrentProfile?.Name ?? "Default"
            };

            var lockedTerms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var collisionTerms = new List<string>();

            foreach (string rawSourceTerm in allTerms.Select(x => x.Source).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string sourceTerm = PromptSafetySanitizer.SanitizeGlossaryConstraint(rawSourceTerm);
                if (string.IsNullOrWhiteSpace(sourceTerm) || PromptSafetySanitizer.IsInstructionLike(sourceTerm))
                {
                    continue;
                }

                string pattern = $@"\b{Regex.Escape(sourceTerm)}\b";
                if (!Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    continue;
                }

                TermResolutionResult resolved = resolver.ResolveTerm(sourceTerm, context);
                if (resolved.RequiresUserSelection || resolved.IsLowConfidenceCollision)
                {
                    collisionTerms.Add(sourceTerm);
                    continue;
                }

                if (resolved.Winner == null || string.IsNullOrWhiteSpace(resolved.Winner.Target))
                {
                    continue;
                }

                string safeTarget = PromptSafetySanitizer.SanitizeGlossaryConstraint(resolved.Winner.Target);
                if (string.IsNullOrWhiteSpace(safeTarget) || PromptSafetySanitizer.IsInstructionLike(safeTarget))
                {
                    continue;
                }

                lockedTerms[sourceTerm] = safeTarget;
            }

            if (collisionTerms.Count > 0)
            {
                _logger.Info("terminology_resolution_collision", new Dictionary<string, string>
                {
                    ["collision_term_count"] = collisionTerms.Count.ToString(),
                    ["domain"] = domain.ToString()
                });
            }

            return lockedTerms
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<string> TryGoogleAsync(string input, string lang, string apiKey, string model, string promptPolicy, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(apiKey)) return "ERROR: Google API Key is missing in Settings.";

            try
            {
                string fullPrompt = $"{promptPolicy}\n\nTranslate this text into {lang}.\n\nText: {input}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = fullPrompt } } }
                    }
                };

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                string jsonContent = JsonSerializer.Serialize(requestBody);

                var response = await httpClient.PostAsync(url, new StringContent(jsonContent, Encoding.UTF8, "application/json"), cancellationToken);
                if (!response.IsSuccessStatusCode) return $"ERROR: Google API ({response.StatusCode})";

                string responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                using JsonDocument doc = JsonDocument.Parse(responseString);

                if (doc.RootElement.TryGetProperty("candidates", out JsonElement candidates) && candidates.GetArrayLength() > 0)
                {
                    var parts = candidates[0].GetProperty("content").GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        return parts[0].GetProperty("text").GetString()?.Trim() ?? string.Empty;
                    }
                }

                return "ERROR: Empty response from Google.";
            }
            catch (Exception ex)
            {
                _logger.Error("provider_google_error", ex);
                return $"ERROR: {ex.Message}";
            }
        }

        private static async Task<string> TryOllamaAsync(string input, string lang, string url, string model, string promptPolicy, CancellationToken cancellationToken)
        {
            try
            {
                if (!url.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase)
                    && !url.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
                {
                    url = url.TrimEnd('/') + "/api/generate";
                }

                string fullPrompt = $"{promptPolicy}\n\nTranslate the following text into {lang}.\n\nText: {input}";
                var requestBody = new
                {
                    model = model,
                    prompt = fullPrompt,
                    stream = false,
                    options = new { temperature = 0.0 }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return $"ERROR: Ollama connection failed ({response.StatusCode}).";
                }

                string responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                using JsonDocument doc = JsonDocument.Parse(responseString);

                if (doc.RootElement.TryGetProperty("response", out JsonElement val))
                {
                    return val.GetString()?.Trim() ?? string.Empty;
                }

                return "ERROR: Unexpected Ollama response format.";
            }
            catch (Exception ex)
            {
                _logger.Error("provider_ollama_error", ex);
                return $"ERROR: Is Ollama running? ({ex.Message})";
            }
        }

        private static async Task<string> TryCustomAsync(string input, string lang, string baseUrl, string apiKey, string model, string promptPolicy, CancellationToken cancellationToken)
        {
            try
            {
                string endpoint = (baseUrl ?? string.Empty).TrimEnd('/');
                if (!endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) endpoint += "/chat/completions";

                string systemContent = $"{promptPolicy}\n\nTranslate the user's input into {lang}.";

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = systemContent },
                        new { role = "user", content = input }
                    },
                    temperature = 0.3
                };

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) return $"ERROR: Custom API ({response.StatusCode})";

                string responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                using JsonDocument doc = JsonDocument.Parse(responseString);

                if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
                {
                    var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                    return content?.Trim() ?? string.Empty;
                }

                return "ERROR: Empty response from Custom API.";
            }
            catch (Exception ex)
            {
                _logger.Error("provider_custom_error", ex);
                return $"ERROR: {ex.Message}";
            }
        }
    }
}
