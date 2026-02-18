using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class GtmConfigService : IGtmConfigService, IDisposable
    {
        private const int CurrentSchemaVersion = 1;
        private const string ConfigDocumentId = "default";

        private readonly object _syncRoot = new();
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<GtmConfigDocument> _configCollection;

        public GtmConfigService(string? basePath = null)
        {
            string resolvedBasePath = basePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");

            Directory.CreateDirectory(resolvedBasePath);
            string dbPath = Path.Combine(resolvedBasePath, "gtm_config.db");

            _database = new LiteDatabase(dbPath);
            _configCollection = _database.GetCollection<GtmConfigDocument>("gtm_config");
        }

        public GtmConfig LoadConfig()
        {
            lock (_syncRoot)
            {
                var document = _configCollection.FindById(ConfigDocumentId);

                if (document == null)
                {
                    var seeded = SeedDefaults();
                    SaveInternal(new GtmConfigDocument
                    {
                        Id = ConfigDocumentId,
                        SchemaVersion = CurrentSchemaVersion,
                        Config = seeded
                    });

                    return seeded;
                }

                if (document.SchemaVersion < CurrentSchemaVersion)
                {
                    document = Migrate(document);
                    SaveInternal(document);
                }

                document.Config ??= SeedDefaults();
                return document.Config;
            }
        }

        public void SaveConfig(GtmConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            lock (_syncRoot)
            {
                var existing = _configCollection.FindById(ConfigDocumentId);
                int nextVersion = (existing?.Config?.ConfigVersion ?? config.ConfigVersion) + 1;
                config.ConfigVersion = Math.Max(1, nextVersion);

                SaveInternal(new GtmConfigDocument
                {
                    Id = ConfigDocumentId,
                    SchemaVersion = CurrentSchemaVersion,
                    Config = config
                });
            }
        }

        public LaunchPhase GetActiveLaunchPhase()
        {
            return LoadConfig().ActiveLaunchPhase;
        }

        public IReadOnlyList<GtmKpiTarget> GetKpiTargetsByPhase(LaunchPhase phase)
        {
            return LoadConfig()
                .KpiTargets
                .Where(x => x.Phase == phase)
                .ToList();
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private void SaveInternal(GtmConfigDocument document)
        {
            _configCollection.Upsert(document);
        }

        private static GtmConfigDocument Migrate(GtmConfigDocument document)
        {
            document.Config ??= SeedDefaults();
            document.SchemaVersion = CurrentSchemaVersion;
            return document;
        }

        private static GtmConfig SeedDefaults()
        {
            return new GtmConfig
            {
                ConfigVersion = 1,
                ActiveLaunchPhase = LaunchPhase.Phase0Readiness,
                CohortSizeTargets = new Dictionary<LaunchPhase, int>
                {
                    [LaunchPhase.Phase0Readiness] = 10,
                    [LaunchPhase.PrivateBeta] = 30,
                    [LaunchPhase.PaidPilot] = 75,
                    [LaunchPhase.Scale] = 200
                },
                KpiTargets = new List<GtmKpiTarget>
                {
                    // Retention
                    new() { MetricKey = "retention_30d", Threshold = 0.60, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "retention_30d", Threshold = 0.70, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "retention_30d", Threshold = 0.80, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "retention_30d", Threshold = 0.88, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Scale },

                    // Conversion
                    new() { MetricKey = "trial_to_paid_conversion", Threshold = 0.10, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "trial_to_paid_conversion", Threshold = 0.20, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "trial_to_paid_conversion", Threshold = 0.30, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "trial_to_paid_conversion", Threshold = 0.40, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Scale },

                    // Latency (ms)
                    new() { MetricKey = "p95_latency_ms", Threshold = 2000, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "p95_latency_ms", Threshold = 1500, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "p95_latency_ms", Threshold = 1000, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "p95_latency_ms", Threshold = 700, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.Scale },

                    // Term quality violations
                    new() { MetricKey = "term_violations_rate", Threshold = 0.05, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "term_violations_rate", Threshold = 0.03, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "term_violations_rate", Threshold = 0.02, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "term_violations_rate", Threshold = 0.01, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.Scale },

                    // PMF dashboard metrics
                    new() { MetricKey = "dau", Threshold = 20, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "dau", Threshold = 80, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "dau", Threshold = 220, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "dau", Threshold = 600, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Scale },

                    new() { MetricKey = "wau", Threshold = 70, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "wau", Threshold = 250, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "wau", Threshold = 700, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "wau", Threshold = 1800, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Scale },

                    new() { MetricKey = "segments_per_day", Threshold = 200, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "segments_per_day", Threshold = 600, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "segments_per_day", Threshold = 1500, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "segments_per_day", Threshold = 4000, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Scale },

                    new() { MetricKey = "retention_week4", Threshold = 0.55, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "retention_week4", Threshold = 0.65, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "retention_week4", Threshold = 0.75, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "retention_week4", Threshold = 0.85, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Scale },

                    new() { MetricKey = "glossary_reuse_rate", Threshold = 0.45, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "glossary_reuse_rate", Threshold = 0.55, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "glossary_reuse_rate", Threshold = 0.62, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "glossary_reuse_rate", Threshold = 0.70, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Scale },

                    new() { MetricKey = "p50_latency_ms", Threshold = 900, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "p50_latency_ms", Threshold = 700, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "p50_latency_ms", Threshold = 500, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "p50_latency_ms", Threshold = 350, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.Scale },

                    new() { MetricKey = "pilot_to_paid_conversion", Threshold = 0.08, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "pilot_to_paid_conversion", Threshold = 0.16, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "pilot_to_paid_conversion", Threshold = 0.24, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "pilot_to_paid_conversion", Threshold = 0.35, ComparisonOperator = KpiComparisonOperator.GreaterThanOrEqual, Phase = LaunchPhase.Scale },

                    new() { MetricKey = "churn_rate", Threshold = 0.12, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.Phase0Readiness },
                    new() { MetricKey = "churn_rate", Threshold = 0.10, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.PrivateBeta },
                    new() { MetricKey = "churn_rate", Threshold = 0.08, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.PaidPilot },
                    new() { MetricKey = "churn_rate", Threshold = 0.06, ComparisonOperator = KpiComparisonOperator.LessThanOrEqual, Phase = LaunchPhase.Scale }
                },
                PricingPlans = new List<PricingPlanDefinition>
                {
                    // Freelancer baseline range: $39-$59/mo
                    new()
                    {
                        PlanId = "freelancer-legal-basic",
                        Segment = CustomerSegment.FreelancerLegal,
                        MonthlyPrice = 39m,
                        Entitlements = new List<string> { "1 seat", "10k words/month", "glossary-learning-basic", "email-support" }
                    },
                    new()
                    {
                        PlanId = "freelancer-legal-pro",
                        Segment = CustomerSegment.FreelancerLegal,
                        MonthlyPrice = 59m,
                        Entitlements = new List<string> { "1 seat", "30k words/month", "advanced-glossary-learning", "priority-email-support" }
                    },

                    // Agency baseline range: $149-$299/mo
                    new()
                    {
                        PlanId = "agency-legal-team",
                        Segment = CustomerSegment.AgencyLegal,
                        MonthlyPrice = 149m,
                        Entitlements = new List<string> { "5 seats", "150k words/month", "shared-glossary", "audit-logs-basic" }
                    },
                    new()
                    {
                        PlanId = "agency-legal-scale",
                        Segment = CustomerSegment.AgencyLegal,
                        MonthlyPrice = 299m,
                        Entitlements = new List<string> { "15 seats", "500k words/month", "shared-glossary", "api-access", "priority-support" }
                    },

                    // Enterprise baseline range: $799-$1499/mo
                    new()
                    {
                        PlanId = "enterprise-legal-core",
                        Segment = CustomerSegment.EnterpriseLegal,
                        MonthlyPrice = 799m,
                        Entitlements = new List<string> { "50 seats", "2M words/month", "sso", "governance-controls", "dedicated-csm" }
                    },
                    new()
                    {
                        PlanId = "enterprise-legal-plus",
                        Segment = CustomerSegment.EnterpriseLegal,
                        MonthlyPrice = 1499m,
                        Entitlements = new List<string> { "200 seats", "unlimited-words", "sso", "advanced-compliance-controls", "priority-sla" }
                    }
                }
            };
        }
    }
}
