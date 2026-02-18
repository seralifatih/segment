using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Segment.App.Services
{
    public class DpapiSecretStore : ISecretStore
    {
        private readonly string _basePath;

        public DpapiSecretStore(string? basePath = null)
        {
            _basePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp", "secrets");
            Directory.CreateDirectory(_basePath);
        }

        public void SetSecret(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Secret key is required.", nameof(key));
            }

            string path = BuildPath(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            byte[] plain = Encoding.UTF8.GetBytes(value);
            byte[] protectedBytes = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, protectedBytes);
        }

        public string GetSecret(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string path = BuildPath(key);
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            try
            {
                byte[] protectedBytes = File.ReadAllBytes(path);
                byte[] plain = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return string.Empty;
            }
        }

        public void DeleteSecret(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            string path = BuildPath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string BuildPath(string key)
        {
            string safe = key.Trim().ToLowerInvariant()
                .Replace(":", "_")
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(" ", "_");
            return Path.Combine(_basePath, $"{safe}.bin");
        }
    }
}
