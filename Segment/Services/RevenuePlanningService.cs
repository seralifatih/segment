using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class RevenuePlanningService : IRevenuePlanningService
    {
        public RevenuePlanningSnapshot BuildSnapshot(
            ArrScenarioPlan plan,
            IReadOnlyList<RevenueActualSnapshot> actuals,
            PmfDashboardSnapshot? latestPmfSnapshot = null,
            PmfDashboardSnapshot? baselinePmfSnapshot = null,
            RevenueRiskThresholds? riskThresholds = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (actuals == null || actuals.Count == 0) throw new ArgumentException("At least one actual snapshot is required.", nameof(actuals));

            var orderedActuals = actuals.OrderBy(x => x.CapturedAtUtc).ToList();
            RevenueActualSnapshot latest = orderedActuals[^1];

            decimal plannedMrr = CalculateMrr(plan.PlannedActivePaidFreelancers, plan.PlannedAgencySeats, plan.PlannedEnterpriseAddOns, plan.Pricing);
            decimal actualMrr = CalculateMrr(latest.ActivePaidFreelancers, latest.AgencySeats, latest.EnterpriseAddOns, plan.Pricing);

            var trend = orderedActuals
                .Select(x =>
                {
                    decimal mrr = CalculateMrr(x.ActivePaidFreelancers, x.AgencySeats, x.EnterpriseAddOns, plan.Pricing);
                    return new RevenueTrendPoint
                    {
                        CapturedAtUtc = x.CapturedAtUtc,
                        ActivePaidFreelancers = x.ActivePaidFreelancers,
                        AgencySeats = x.AgencySeats,
                        EnterpriseAddOns = x.EnterpriseAddOns,
                        MrrUsd = mrr,
                        ArrUsd = mrr * 12m
                    };
                })
                .ToList();

            var projections = BuildProjections(plan, orderedActuals, latest);
            RevenueRiskAssessment risks = BuildRisks(latestPmfSnapshot, baselinePmfSnapshot, riskThresholds ?? new RevenueRiskThresholds());

            return new RevenuePlanningSnapshot
            {
                ScenarioType = plan.ScenarioType,
                TargetArrUsd = plan.TargetArrUsd,
                PlannedActivePaidFreelancers = plan.PlannedActivePaidFreelancers,
                PlannedAgencySeats = plan.PlannedAgencySeats,
                PlannedEnterpriseAddOns = plan.PlannedEnterpriseAddOns,
                ActualActivePaidFreelancers = latest.ActivePaidFreelancers,
                ActualAgencySeats = latest.AgencySeats,
                ActualEnterpriseAddOns = latest.EnterpriseAddOns,
                PlannedMrrUsd = plannedMrr,
                PlannedArrUsd = plannedMrr * 12m,
                ActualMrrUsd = actualMrr,
                ActualArrUsd = actualMrr * 12m,
                ArrGapUsd = plan.TargetArrUsd - (actualMrr * 12m),
                Trend = trend,
                Projections = projections,
                Risks = risks
            };
        }

        private static List<RevenueProjectionPoint> BuildProjections(
            ArrScenarioPlan plan,
            IReadOnlyList<RevenueActualSnapshot> orderedActuals,
            RevenueActualSnapshot latest)
        {
            (double freelancerGrowth, double seatGrowth, double enterpriseGrowth) = ResolveGrowthRates(plan, orderedActuals);
            var projections = new List<RevenueProjectionPoint>(3);
            int[] horizons = { 3, 6, 12 };

            foreach (int horizon in horizons)
            {
                int projectedFreelancers = ProjectCount(latest.ActivePaidFreelancers, freelancerGrowth, horizon);
                int projectedSeats = ProjectCount(latest.AgencySeats, seatGrowth, horizon);
                int projectedEnterprise = ProjectCount(latest.EnterpriseAddOns, enterpriseGrowth, horizon);
                decimal projectedMrr = CalculateMrr(projectedFreelancers, projectedSeats, projectedEnterprise, plan.Pricing);
                decimal projectedArr = projectedMrr * 12m;
                decimal gapToTargetArr = Math.Max(0m, plan.TargetArrUsd - projectedArr);
                RevenueRequiredConversions requiredConversions = CalculateRequiredConversions(gapToTargetArr, plan.ScenarioType, plan.Pricing);

                projections.Add(new RevenueProjectionPoint
                {
                    HorizonMonths = horizon,
                    ProjectedMrrUsd = projectedMrr,
                    ProjectedArrUsd = projectedArr,
                    GapToTargetArrUsd = gapToTargetArr,
                    RequiredConversions = requiredConversions
                });
            }

            return projections;
        }

        private static RevenueRequiredConversions CalculateRequiredConversions(
            decimal gapToTargetArrUsd,
            ArrScenarioType scenario,
            RevenuePricingAssumptions pricing)
        {
            if (gapToTargetArrUsd <= 0m)
            {
                return new RevenueRequiredConversions();
            }

            decimal gapMrr = gapToTargetArrUsd / 12m;
            (decimal freelancerWeight, decimal agencyWeight, decimal enterpriseWeight) = GetScenarioWeights(scenario);

            int requiredFreelancers = pricing.FreelancerMrrUsd <= 0m
                ? 0
                : (int)Math.Ceiling((double)((gapMrr * freelancerWeight) / pricing.FreelancerMrrUsd));
            int requiredAgencySeats = pricing.AgencySeatMrrUsd <= 0m
                ? 0
                : (int)Math.Ceiling((double)((gapMrr * agencyWeight) / pricing.AgencySeatMrrUsd));
            int requiredEnterprise = pricing.EnterpriseAddOnMrrUsd <= 0m
                ? 0
                : (int)Math.Ceiling((double)((gapMrr * enterpriseWeight) / pricing.EnterpriseAddOnMrrUsd));

            return new RevenueRequiredConversions
            {
                RequiredPaidFreelancers = Math.Max(0, requiredFreelancers),
                RequiredAgencySeats = Math.Max(0, requiredAgencySeats),
                RequiredEnterpriseAddOns = Math.Max(0, requiredEnterprise)
            };
        }

        private static RevenueRiskAssessment BuildRisks(
            PmfDashboardSnapshot? latest,
            PmfDashboardSnapshot? baseline,
            RevenueRiskThresholds thresholds)
        {
            var risks = new RevenueRiskAssessment();
            if (latest == null)
            {
                return risks;
            }

            risks.ChurnAboveThreshold = latest.ChurnRate > thresholds.ChurnRateThreshold;
            if (risks.ChurnAboveThreshold)
            {
                risks.Alerts.Add($"Churn risk: {latest.ChurnRate:P2} exceeds threshold {thresholds.ChurnRateThreshold:P2}.");
            }

            risks.ConversionBelowThreshold = latest.PilotToPaidConversion < thresholds.ConversionRateThreshold;
            if (risks.ConversionBelowThreshold)
            {
                risks.Alerts.Add($"Conversion risk: {latest.PilotToPaidConversion:P2} below threshold {thresholds.ConversionRateThreshold:P2}.");
            }

            if (baseline != null && baseline.P95LatencyMs > 0)
            {
                double latencyIncreaseRatio = (latest.P95LatencyMs - baseline.P95LatencyMs) / baseline.P95LatencyMs;
                risks.LatencyRegressionRisk = latencyIncreaseRatio > thresholds.LatencyRegressionThresholdRatio;
                if (risks.LatencyRegressionRisk)
                {
                    risks.Alerts.Add($"Expansion risk: P95 latency regressed from {baseline.P95LatencyMs:F0}ms to {latest.P95LatencyMs:F0}ms.");
                }
            }

            if (baseline != null && baseline.TerminologyViolationRate > 0)
            {
                double qualityRegressionRatio = (latest.TerminologyViolationRate - baseline.TerminologyViolationRate) / baseline.TerminologyViolationRate;
                risks.QualityRegressionRisk = qualityRegressionRatio > thresholds.QualityRegressionThresholdRatio;
                if (risks.QualityRegressionRisk)
                {
                    risks.Alerts.Add($"Expansion risk: terminology violation rate regressed from {baseline.TerminologyViolationRate:P2} to {latest.TerminologyViolationRate:P2}.");
                }
            }

            return risks;
        }

        private static (decimal FreelancerWeight, decimal AgencyWeight, decimal EnterpriseWeight) GetScenarioWeights(ArrScenarioType scenario)
        {
            return scenario switch
            {
                ArrScenarioType.FreelancerLed => (0.70m, 0.25m, 0.05m),
                ArrScenarioType.AgencyLed => (0.20m, 0.65m, 0.15m),
                _ => (0.45m, 0.40m, 0.15m)
            };
        }

        private static (double FreelancerGrowth, double SeatGrowth, double EnterpriseGrowth) ResolveGrowthRates(
            ArrScenarioPlan plan,
            IReadOnlyList<RevenueActualSnapshot> orderedActuals)
        {
            if (orderedActuals.Count < 2)
            {
                return (
                    plan.MonthlyFreelancerGrowthRate,
                    plan.MonthlyAgencySeatGrowthRate,
                    plan.MonthlyEnterpriseAddOnGrowthRate);
            }

            RevenueActualSnapshot previous = orderedActuals[^2];
            RevenueActualSnapshot latest = orderedActuals[^1];

            double observedFreelancerGrowth = ComputeObservedGrowth(previous.ActivePaidFreelancers, latest.ActivePaidFreelancers);
            double observedSeatGrowth = ComputeObservedGrowth(previous.AgencySeats, latest.AgencySeats);
            double observedEnterpriseGrowth = ComputeObservedGrowth(previous.EnterpriseAddOns, latest.EnterpriseAddOns);

            return (
                (plan.MonthlyFreelancerGrowthRate + observedFreelancerGrowth) / 2.0,
                (plan.MonthlyAgencySeatGrowthRate + observedSeatGrowth) / 2.0,
                (plan.MonthlyEnterpriseAddOnGrowthRate + observedEnterpriseGrowth) / 2.0);
        }

        private static double ComputeObservedGrowth(int previous, int latest)
        {
            if (previous <= 0 || latest <= 0)
            {
                return 0;
            }

            return ((double)latest - previous) / previous;
        }

        private static int ProjectCount(int currentCount, double growthRate, int months)
        {
            if (currentCount <= 0)
            {
                return 0;
            }

            double adjustedGrowth = Math.Max(-0.95, growthRate);
            double projected = currentCount * Math.Pow(1.0 + adjustedGrowth, months);
            return Math.Max(0, (int)Math.Round(projected, MidpointRounding.AwayFromZero));
        }

        private static decimal CalculateMrr(int freelancers, int agencySeats, int enterpriseAddOns, RevenuePricingAssumptions pricing)
        {
            int safeFreelancers = Math.Max(0, freelancers);
            int safeAgencySeats = Math.Max(0, agencySeats);
            int safeEnterpriseAddOns = Math.Max(0, enterpriseAddOns);

            return (safeFreelancers * pricing.FreelancerMrrUsd)
                + (safeAgencySeats * pricing.AgencySeatMrrUsd)
                + (safeEnterpriseAddOns * pricing.EnterpriseAddOnMrrUsd);
        }
    }
}
