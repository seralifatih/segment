using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class ReferralService : IReferralService, IAttributionAnalyticsService, IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<ReferralCodeRecord> _referralCodes;
        private readonly ILiteCollection<ReferredUserRecord> _referredUsers;
        private readonly ILiteCollection<ReferralMilestoneRecord> _milestones;
        private readonly ILiteCollection<ReferralRewardRecord> _rewards;
        private readonly ILiteCollection<GlossaryPackImportRecord> _glossaryImports;
        private readonly ILiteCollection<AgencyDomainActivityRecord> _agencyDomainActivity;

        public ReferralService(string? basePath = null)
        {
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");

            Directory.CreateDirectory(resolvedBasePath);
            string dbPath = Path.Combine(resolvedBasePath, "viral_loops.db");

            _database = new LiteDatabase(dbPath);
            _referralCodes = _database.GetCollection<ReferralCodeRecord>("referral_codes");
            _referredUsers = _database.GetCollection<ReferredUserRecord>("referred_users");
            _milestones = _database.GetCollection<ReferralMilestoneRecord>("referral_milestones");
            _rewards = _database.GetCollection<ReferralRewardRecord>("referral_rewards");
            _glossaryImports = _database.GetCollection<GlossaryPackImportRecord>("glossary_pack_imports");
            _agencyDomainActivity = _database.GetCollection<AgencyDomainActivityRecord>("agency_domain_activity");

            _referralCodes.EnsureIndex(x => x.Code, unique: true);
            _referredUsers.EnsureIndex(x => x.UserId, unique: true);
            _rewards.EnsureIndex(x => x.ReferredUserId);
            _glossaryImports.EnsureIndex(x => x.ReferralCode);
            _agencyDomainActivity.EnsureIndex(x => x.Domain, unique: true);
        }

        public string CreateReferralCode(string referrerUserId)
        {
            if (string.IsNullOrWhiteSpace(referrerUserId))
            {
                throw new ArgumentException("Referrer user ID is required.", nameof(referrerUserId));
            }

            lock (_syncRoot)
            {
                string code = BuildCode(referrerUserId);
                _referralCodes.Upsert(new ReferralCodeRecord
                {
                    Code = code,
                    ReferrerUserId = referrerUserId,
                    CreatedAtUtc = DateTime.UtcNow
                });
                return code;
            }
        }

        public string BuildReferralLink(string referralCode, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(referralCode))
            {
                throw new ArgumentException("Referral code is required.", nameof(referralCode));
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL is required.", nameof(baseUrl));
            }

            string trimmed = baseUrl.TrimEnd('/');
            return $"{trimmed}/ref/{referralCode}";
        }

        public void RegisterReferredUser(string referredUserId, string referralCode, DateTime signupAtUtc, string emailDomain)
        {
            if (string.IsNullOrWhiteSpace(referredUserId))
            {
                throw new ArgumentException("Referred user ID is required.", nameof(referredUserId));
            }
            if (string.IsNullOrWhiteSpace(referralCode))
            {
                throw new ArgumentException("Referral code is required.", nameof(referralCode));
            }

            lock (_syncRoot)
            {
                string normalizedReferredUserId = referredUserId.Trim();
                var code = _referralCodes.FindById(referralCode.Trim());
                if (code == null)
                {
                    throw new InvalidOperationException($"Unknown referral code: {referralCode}");
                }

                DateTime normalizedSignupAtUtc = signupAtUtc.ToUniversalTime();
                if (normalizedSignupAtUtc > DateTime.UtcNow.AddMinutes(5))
                {
                    normalizedSignupAtUtc = DateTime.UtcNow;
                }

                _referredUsers.Upsert(new ReferredUserRecord
                {
                    UserId = normalizedReferredUserId,
                    ReferralCode = referralCode.Trim(),
                    ReferrerUserId = code.ReferrerUserId,
                    SignupAtUtc = normalizedSignupAtUtc,
                    EmailDomain = NormalizeDomain(emailDomain)
                });

                _milestones.Upsert(new ReferralMilestoneRecord { UserId = normalizedReferredUserId, LastUpdatedUtc = DateTime.UtcNow });
                TrackFreelancerDomainActivation(normalizedReferredUserId, emailDomain);
            }
        }

        public void RecordGlossaryImportedMilestone(string referredUserId, DateTime? occurredAtUtc = null)
        {
            lock (_syncRoot)
            {
                string userId = NormalizeRequiredUserId(referredUserId);
                EnsureReferredUserExists(userId);
                var milestone = GetOrCreateMilestone(userId);
                milestone.GlossaryImportedAtUtc = occurredAtUtc ?? DateTime.UtcNow;
                milestone.LastUpdatedUtc = DateTime.UtcNow;
                _milestones.Upsert(milestone);
            }
        }

        public void RecordTranslatedSegmentsMilestone(string referredUserId, int additionalSegments, DateTime? occurredAtUtc = null)
        {
            if (additionalSegments <= 0) return;

            lock (_syncRoot)
            {
                string userId = NormalizeRequiredUserId(referredUserId);
                EnsureReferredUserExists(userId);
                var milestone = GetOrCreateMilestone(userId);
                milestone.TranslatedSegmentCount += additionalSegments;
                milestone.LastTranslatedAtUtc = occurredAtUtc ?? DateTime.UtcNow;
                milestone.LastUpdatedUtc = DateTime.UtcNow;
                _milestones.Upsert(milestone);
            }
        }

        public void RecordPaidConversionMilestone(string referredUserId, DateTime? occurredAtUtc = null)
        {
            lock (_syncRoot)
            {
                string userId = NormalizeRequiredUserId(referredUserId);
                EnsureReferredUserExists(userId);
                var milestone = GetOrCreateMilestone(userId);
                milestone.PaidConversionAtUtc = occurredAtUtc ?? DateTime.UtcNow;
                milestone.LastUpdatedUtc = DateTime.UtcNow;
                _milestones.Upsert(milestone);
            }
        }

        public ReferralRewardEligibilityResult GrantRewardIfEligible(string referredUserId, int requiredTranslatedSegments, int conversionWindowDays)
        {
            if (requiredTranslatedSegments < 0) requiredTranslatedSegments = 0;
            if (conversionWindowDays <= 0) conversionWindowDays = 14;

            lock (_syncRoot)
            {
                string userId = NormalizeRequiredUserId(referredUserId);
                var referred = _referredUsers.FindById(userId);
                if (referred == null)
                {
                    return new ReferralRewardEligibilityResult
                    {
                        Eligible = false,
                        Reason = "Referred user not registered.",
                        ReferredUserId = userId
                    };
                }

                var milestone = _milestones.FindById(userId);
                if (milestone == null)
                {
                    return new ReferralRewardEligibilityResult
                    {
                        Eligible = false,
                        Reason = "Milestones are not complete.",
                        ReferrerUserId = referred.ReferrerUserId,
                        ReferredUserId = userId
                    };
                }

                bool alreadyRewarded = _rewards.Exists(x => x.ReferredUserId == userId);
                if (alreadyRewarded)
                {
                    return new ReferralRewardEligibilityResult
                    {
                        Eligible = true,
                        RewardGranted = false,
                        AlreadyRewarded = true,
                        Reason = "Reward already granted.",
                        ReferrerUserId = referred.ReferrerUserId,
                        ReferredUserId = userId
                    };
                }

                bool glossaryImported = milestone.GlossaryImportedAtUtc.HasValue;
                bool translatedEnough = milestone.TranslatedSegmentCount >= requiredTranslatedSegments;
                bool paidConverted = milestone.PaidConversionAtUtc.HasValue;
                bool conversionWithinWindow = paidConverted &&
                    (milestone.PaidConversionAtUtc!.Value - referred.SignupAtUtc).TotalDays <= conversionWindowDays;

                if (!glossaryImported || !translatedEnough || !paidConverted || !conversionWithinWindow)
                {
                    return new ReferralRewardEligibilityResult
                    {
                        Eligible = false,
                        RewardGranted = false,
                        AlreadyRewarded = false,
                        Reason = "Referral quality milestones are not satisfied.",
                        ReferrerUserId = referred.ReferrerUserId,
                        ReferredUserId = userId
                    };
                }

                _rewards.Insert(new ReferralRewardRecord
                {
                    ReferredUserId = userId,
                    ReferrerUserId = referred.ReferrerUserId,
                    AwardedAtUtc = DateTime.UtcNow,
                    RequiredTranslatedSegments = requiredTranslatedSegments,
                    ConversionWindowDays = conversionWindowDays
                });

                return new ReferralRewardEligibilityResult
                {
                    Eligible = true,
                    RewardGranted = true,
                    AlreadyRewarded = false,
                    Reason = "Reward granted.",
                    ReferrerUserId = referred.ReferrerUserId,
                    ReferredUserId = userId
                };
            }
        }

        public ReferralConversionFunnelDashboard GetReferralConversionDashboard()
        {
            lock (_syncRoot)
            {
                int registered = _referredUsers.Count();
                var milestones = _milestones.FindAll().ToList();

                int glossaryImported = milestones.Count(x => x.GlossaryImportedAtUtc.HasValue);
                int translatedQualified = milestones.Count(x => x.TranslatedSegmentCount > 0);
                int paidConverted = milestones.Count(x => x.PaidConversionAtUtc.HasValue);
                int rewarded = _rewards.Count();

                return new ReferralConversionFunnelDashboard
                {
                    RegisteredUsers = registered,
                    GlossaryImportedUsers = glossaryImported,
                    TranslationQualifiedUsers = translatedQualified,
                    PaidConvertedUsers = paidConverted,
                    RewardGrantedUsers = rewarded,
                    GlossaryImportRate = registered == 0 ? 0 : (double)glossaryImported / registered,
                    TranslationQualifiedRate = registered == 0 ? 0 : (double)translatedQualified / registered,
                    PaidConversionRate = registered == 0 ? 0 : (double)paidConverted / registered,
                    RewardGrantRate = registered == 0 ? 0 : (double)rewarded / registered
                };
            }
        }

        public void RecordGlossaryPackImport(GlossaryPackImportRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            lock (_syncRoot)
            {
                _glossaryImports.Insert(record);
            }
        }

        public AttributionAnalyticsSnapshot GetAttributionSnapshot()
        {
            lock (_syncRoot)
            {
                var importsByCode = _glossaryImports
                    .FindAll()
                    .Where(x => !string.IsNullOrWhiteSpace(x.ReferralCode))
                    .GroupBy(x => x.ReferralCode)
                    .ToDictionary(g => g.Key, g => g.Count());

                return new AttributionAnalyticsSnapshot
                {
                    TotalGlossaryPackImports = _glossaryImports.Count(),
                    TotalReferralRewardsGranted = _rewards.Count(),
                    ImportsByReferralCode = importsByCode
                };
            }
        }

        public void TrackFreelancerDomainActivation(string userId, string emailDomain)
        {
            string domain = NormalizeDomain(emailDomain);
            if (string.IsNullOrWhiteSpace(domain)) return;

            lock (_syncRoot)
            {
                var activity = _agencyDomainActivity.FindById(domain) ?? new AgencyDomainActivityRecord
                {
                    Domain = domain,
                    FreelancerUserIds = new List<string>()
                };

                if (!activity.FreelancerUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase))
                {
                    activity.FreelancerUserIds.Add(userId);
                }

                _agencyDomainActivity.Upsert(activity);
            }
        }

        public AgencyExpansionTriggerResult EvaluateAgencyExpansion(string emailDomain, int freelancerThreshold = 3)
        {
            string domain = NormalizeDomain(emailDomain);
            if (string.IsNullOrWhiteSpace(domain))
            {
                return new AgencyExpansionTriggerResult
                {
                    Triggered = false,
                    Message = "Domain is required."
                };
            }

            lock (_syncRoot)
            {
                var activity = _agencyDomainActivity.FindById(domain);
                int uniqueFreelancers = activity?.FreelancerUserIds.Count ?? 0;
                bool triggered = uniqueFreelancers >= freelancerThreshold;

                return new AgencyExpansionTriggerResult
                {
                    Triggered = triggered,
                    Domain = domain,
                    UniqueFreelancerCount = uniqueFreelancers,
                    SuggestedPlan = PricingPlan.LegalTeam,
                    Message = triggered
                        ? $"Detected {uniqueFreelancers} freelancers at {domain}. Prompt Legal Team upgrade."
                        : $"Detected {uniqueFreelancers} freelancers at {domain}. Threshold is {freelancerThreshold}."
                };
            }
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private ReferralMilestoneRecord GetOrCreateMilestone(string userId)
        {
            return _milestones.FindById(userId) ?? new ReferralMilestoneRecord
            {
                UserId = userId,
                LastUpdatedUtc = DateTime.UtcNow
            };
        }

        private static string BuildCode(string referrerUserId)
        {
            string seed = referrerUserId.Length > 6 ? referrerUserId[..6] : referrerUserId;
            string suffix = Guid.NewGuid().ToString("N")[..8];
            return $"{seed.ToUpperInvariant()}-{suffix.ToUpperInvariant()}";
        }

        private static string NormalizeDomain(string emailDomain)
        {
            if (string.IsNullOrWhiteSpace(emailDomain)) return "";
            string domain = emailDomain.Trim().ToLowerInvariant();
            int atIndex = domain.IndexOf('@');
            if (atIndex >= 0 && atIndex < domain.Length - 1)
            {
                domain = domain[(atIndex + 1)..];
            }

            return domain;
        }

        private string NormalizeRequiredUserId(string referredUserId)
        {
            if (string.IsNullOrWhiteSpace(referredUserId))
            {
                throw new ArgumentException("Referred user ID is required.", nameof(referredUserId));
            }

            return referredUserId.Trim();
        }

        private void EnsureReferredUserExists(string userId)
        {
            if (_referredUsers.FindById(userId) == null)
            {
                throw new InvalidOperationException($"Referred user is not registered: {userId}");
            }
        }

        private class ReferralCodeRecord
        {
            [BsonId]
            public string Code { get; set; } = "";
            public string ReferrerUserId { get; set; } = "";
            public DateTime CreatedAtUtc { get; set; }
        }

        private class ReferredUserRecord
        {
            [BsonId]
            public string UserId { get; set; } = "";
            public string ReferralCode { get; set; } = "";
            public string ReferrerUserId { get; set; } = "";
            public DateTime SignupAtUtc { get; set; }
            public string EmailDomain { get; set; } = "";
        }

        private class ReferralMilestoneRecord
        {
            [BsonId]
            public string UserId { get; set; } = "";
            public DateTime? GlossaryImportedAtUtc { get; set; }
            public int TranslatedSegmentCount { get; set; }
            public DateTime? LastTranslatedAtUtc { get; set; }
            public DateTime? PaidConversionAtUtc { get; set; }
            public DateTime LastUpdatedUtc { get; set; }
        }

        private class ReferralRewardRecord
        {
            [BsonId]
            public ObjectId Id { get; set; } = ObjectId.NewObjectId();
            public string ReferredUserId { get; set; } = "";
            public string ReferrerUserId { get; set; } = "";
            public DateTime AwardedAtUtc { get; set; }
            public int RequiredTranslatedSegments { get; set; }
            public int ConversionWindowDays { get; set; }
        }

        private class AgencyDomainActivityRecord
        {
            [BsonId]
            public string Domain { get; set; } = "";
            public List<string> FreelancerUserIds { get; set; } = new();
        }
    }
}
