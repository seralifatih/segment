using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System;

namespace Segment.App.Services
{
    public class AppConfig
    {
        public string TargetLanguage { get; set; } = "Turkish";

        // Seçenekler: "Google", "Ollama", "Custom"
        public string AiProvider { get; set; } = "Google";
        public string SecondaryAiProvider { get; set; } = "";

        // --- Google ---
        public string GoogleApiKey { get; set; } = "";
        public string GoogleModel { get; set; } = "gemma-3-27b-it";

        // --- Ollama (Local) ---
        public string OllamaUrl { get; set; } = "http://localhost:11434/api/generate";
        public string OllamaModel { get; set; } = "llama3.2";

        // --- CUSTOM / OPENAI COMPATIBLE (YENİ) ---
        // DeepSeek, Groq, OpenRouter, OpenAI vb. hepsi buraya girer.
        public string CustomBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string CustomApiKey { get; set; } = "";
        public string CustomModel { get; set; } = "gpt-4o-mini";

        // --- First Run Flag ---
        public bool IsFirstRun { get; set; } = true;

        // --- Launch gating user context ---
        public bool IsAdminUser { get; set; } = false;
        public bool IsInvitedUser { get; set; } = false;
        public bool IsLegalNicheUser { get; set; } = true;
        public bool IsAgencyAccount { get; set; } = false;
        public bool HasPilotContract { get; set; } = false;
        public string ActiveDomain { get; set; } = "Legal";

        // --- Pricing/packaging selection ---
        public string ActivePricingPlan { get; set; } = "LegalProIndividual";
        public string ActiveBillingInterval { get; set; } = "Monthly";
        public int ActiveSeatCount { get; set; } = 1;
        public bool ApplyPlatformFee { get; set; } = false;

        // --- Partnership-led GTM ---
        public bool PilotWorkspaceModeEnabled { get; set; } = false;
        public bool DemoDatasetModeEnabled { get; set; } = false;
        public string AccountId { get; set; } = "account-local";
        public string AccountDisplayName { get; set; } = "Local Account";
        public string PartnerTagsCsv { get; set; } = "";
        public string ActivePilotWorkspaceId { get; set; } = "";

        // --- Compliance and data handling ---
        public string ConfidentialityMode { get; set; } = "Standard";
        public bool ConfidentialProjectLocalOnly { get; set; } = false;
        public bool AllowGuardrailOverrides { get; set; } = false;
        public string RetentionPolicySummary { get; set; } = "Glossary and audit records are retained locally. Cloud telemetry is opt-in by category.";

        // --- Telemetry consent granularity ---
        public bool TelemetryUsageMetricsConsent { get; set; } = false;
        public bool TelemetryCrashDiagnosticsConsent { get; set; } = true;
        public bool TelemetryModelOutputConsent { get; set; } = false;
        public bool TelemetryConsentLockEnabled { get; set; } = false;
        public string TelemetryConsentLockedByAccountId { get; set; } = "";
        public bool MinimizeDiagnosticLogging { get; set; } = false;
        public bool EnforceApprovedProviders { get; set; } = false;
        public string ApprovedProvidersCsv { get; set; } = "Google,Ollama,Custom";
        public bool PreferLocalProcessingPath { get; set; } = false;
        public bool LearningSuggestOnly { get; set; } = true;
        public bool RequireExplicitSharedPromotionApproval { get; set; } = true;
        public bool QaStrictMode { get; set; } = false;

        // --- Reflex hotkeys ---
        public string ShowPanelHotkey { get; set; } = "Ctrl+Space";
        public string TranslateSelectionInPlaceHotkey { get; set; } = "Ctrl+Shift+Space";
    }

    public static class SettingsService
    {
        private static string _settingsPath = "settings.json";
        private static readonly StructuredLogger Logger = new StructuredLogger();
        private const string GoogleApiKeySecret = "provider.google.api_key";
        private const string CustomApiKeySecret = "provider.custom.api_key";

        public static ISecretStore SecretStore { get; private set; } = new DpapiSecretStore();
        public static AppConfig Current { get; private set; }

        static SettingsService() { Load(); }

        public static void ConfigureStorageForTests(string settingsPath, ISecretStore? secretStore = null)
        {
            _settingsPath = string.IsNullOrWhiteSpace(settingsPath) ? "settings.json" : settingsPath;
            SecretStore = secretStore ?? SecretStore;
            Load();
        }

        public static void ResetStorageConfiguration()
        {
            _settingsPath = "settings.json";
            SecretStore = new DpapiSecretStore();
            Load();
        }

        public static void Load()
        {
            if (File.Exists(_settingsPath))
            {
                try { Current = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_settingsPath)) ?? new AppConfig(); }
                catch { Current = new AppConfig(); }
            }
            else { Current = new AppConfig(); Save(); }

            MigrateLegacySecrets();
            Current.GoogleApiKey = SecretStore.GetSecret(GoogleApiKeySecret);
            Current.CustomApiKey = SecretStore.GetSecret(CustomApiKeySecret);
        }

        public static void Save()
        {
            SecretStore.SetSecret(GoogleApiKeySecret, Current.GoogleApiKey ?? string.Empty);
            SecretStore.SetSecret(CustomApiKeySecret, Current.CustomApiKey ?? string.Empty);

            string json = JsonSerializer.Serialize(Current);
            var copy = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            copy.GoogleApiKey = string.Empty;
            copy.CustomApiKey = string.Empty;
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static IReadOnlySet<string> GetApprovedProviders()
        {
            string csv = Current.ApprovedProvidersCsv ?? string.Empty;
            var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                providers.Add(token);
            }

            return providers;
        }

        private static void MigrateLegacySecrets()
        {
            try
            {
                int migrated = 0;
                if (!string.IsNullOrWhiteSpace(Current.GoogleApiKey))
                {
                    SecretStore.SetSecret(GoogleApiKeySecret, Current.GoogleApiKey);
                    Current.GoogleApiKey = string.Empty;
                    migrated++;
                }

                if (!string.IsNullOrWhiteSpace(Current.CustomApiKey))
                {
                    SecretStore.SetSecret(CustomApiKeySecret, Current.CustomApiKey);
                    Current.CustomApiKey = string.Empty;
                    migrated++;
                }

                if (migrated > 0)
                {
                    Save();
                    Logger.Info("secret_migration_success", new Dictionary<string, string>
                    {
                        ["migrated_secret_count"] = migrated.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("secret_migration_failed", ex);
            }
        }
    }
}
