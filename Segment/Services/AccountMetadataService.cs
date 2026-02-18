using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class AccountMetadataService : IAccountMetadataService, IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<AccountMetadata> _accounts;

        public AccountMetadataService(string? basePath = null)
        {
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");

            Directory.CreateDirectory(resolvedBasePath);
            string dbPath = Path.Combine(resolvedBasePath, "partner_gtm.db");

            _database = new LiteDatabase($"Filename={dbPath};Connection=shared");
            _accounts = _database.GetCollection<AccountMetadata>("account_metadata");
            _accounts.EnsureIndex(x => x.AccountId, unique: true);
        }

        public AccountMetadata GetOrCreate(string accountId, string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Account ID is required.", nameof(accountId));
            }

            string normalizedId = accountId.Trim();
            var existing = _accounts.FindById(normalizedId);
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(displayName) && !string.Equals(existing.DisplayName, displayName.Trim(), StringComparison.Ordinal))
                {
                    existing.DisplayName = displayName.Trim();
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                    _accounts.Upsert(existing);
                }

                return existing;
            }

            var created = new AccountMetadata
            {
                AccountId = normalizedId,
                DisplayName = displayName?.Trim() ?? normalizedId,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _accounts.Insert(created);
            return created;
        }

        public AccountMetadata SetPartnerTags(string accountId, IEnumerable<string> tags, string? displayName = null)
        {
            var metadata = GetOrCreate(accountId, displayName);
            metadata.PartnerTags = (tags ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            metadata.UpdatedAtUtc = DateTime.UtcNow;
            _accounts.Upsert(metadata);
            return metadata;
        }

        public void Dispose()
        {
            _database.Dispose();
        }
    }
}
