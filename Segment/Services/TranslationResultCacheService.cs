using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Segment.App.Services
{
    public class TranslationResultCacheService
    {
        private sealed class CacheEntry
        {
            public string Value { get; set; } = "";
            public DateTime ExpiresAtUtc { get; set; }
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
        private readonly TimeSpan _ttl;

        public TranslationResultCacheService(int ttlMinutes = 20)
        {
            _ttl = TimeSpan.FromMinutes(Math.Max(1, ttlMinutes));
        }

        public bool TryGet(string key, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (!_cache.TryGetValue(key, out CacheEntry? entry))
            {
                return false;
            }

            if (DateTime.UtcNow > entry.ExpiresAtUtc)
            {
                _cache.TryRemove(key, out _);
                return false;
            }

            value = entry.Value;
            return true;
        }

        public void Set(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            _cache[key] = new CacheEntry
            {
                Value = value,
                ExpiresAtUtc = DateTime.UtcNow.Add(_ttl)
            };
        }

        public static string BuildKey(
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            string glossaryVersion,
            string domain)
        {
            string textHash = ComputeHash(sourceText ?? string.Empty);
            return $"{textHash}|{(sourceLanguage ?? "").Trim()}|{(targetLanguage ?? "").Trim()}|{(glossaryVersion ?? "").Trim()}|{(domain ?? "").Trim()}";
        }

        private static string ComputeHash(string text)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant()[..24];
        }
    }
}
