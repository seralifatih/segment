using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class SettingsServiceSecretPersistenceTests
    {
        [Fact]
        public void Save_Should_Not_Persist_ApiKeys_In_Plaintext_Settings()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "segment_settings_tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string settingsPath = Path.Combine(tempDir, "settings.json");
            var secrets = new InMemorySecretStore();
            try
            {
                SettingsService.ConfigureStorageForTests(settingsPath, secrets);
                SettingsService.Current.GoogleApiKey = "google-secret-123";
                SettingsService.Current.CustomApiKey = "custom-secret-xyz";
                SettingsService.Save();

                string persisted = File.ReadAllText(settingsPath);
                persisted.Should().NotContain("google-secret-123");
                persisted.Should().NotContain("custom-secret-xyz");

                secrets.GetSecret("provider.google.api_key").Should().Be("google-secret-123");
                secrets.GetSecret("provider.custom.api_key").Should().Be("custom-secret-xyz");
            }
            finally
            {
                SettingsService.ResetStorageConfiguration();
            }
        }

        private sealed class InMemorySecretStore : ISecretStore
        {
            private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

            public void SetSecret(string key, string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _secrets.Remove(key);
                    return;
                }

                _secrets[key] = value;
            }

            public string GetSecret(string key)
            {
                return _secrets.TryGetValue(key, out string value) ? value : string.Empty;
            }

            public void DeleteSecret(string key)
            {
                _secrets.Remove(key);
            }
        }
    }
}
