using System;
using System.Collections.Generic;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class RevenuePlanningServiceTests
    {
        [Fact]
        public void BuildSnapshot_Should_Calculate_Actual_And_Planned_Mrr_Arr()
        {
            var plan = new ArrScenarioPlan
            {
                ScenarioType = ArrScenarioType.Mixed,
                TargetArrUsd = 1_000_000m,
                PlannedActivePaidFreelancers = 500,
                PlannedAgencySeats = 700,
                PlannedEnterpriseAddOns = 15,
                Pricing = new RevenuePricingAssumptions
                {
                    FreelancerMrrUsd = 80m,
                    AgencySeatMrrUsd = 50m,
                    EnterpriseAddOnMrrUsd = 1_500m
                }
            };

            var actuals = new List<RevenueActualSnapshot>
            {
                new()
                {
                    CapturedAtUtc = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
                    ActivePaidFreelancers = 200,
                    AgencySeats = 250,
                    EnterpriseAddOns = 5
                }
            };

            var service = new RevenuePlanningService();
            RevenuePlanningSnapshot snapshot = service.BuildSnapshot(plan, actuals);

            snapshot.ActualMrrUsd.Should().Be(36_000m);
            snapshot.ActualArrUsd.Should().Be(432_000m);
            snapshot.PlannedMrrUsd.Should().Be(97_500m);
            snapshot.PlannedArrUsd.Should().Be(1_170_000m);
            snapshot.ArrGapUsd.Should().Be(568_000m);
        }

        [Fact]
        public void BuildSnapshot_Should_Generate_Three_Six_Twelve_Month_Projections()
        {
            var plan = new ArrScenarioPlan
            {
                ScenarioType = ArrScenarioType.FreelancerLed,
                PlannedActivePaidFreelancers = 300,
                PlannedAgencySeats = 300,
                PlannedEnterpriseAddOns = 8,
                MonthlyFreelancerGrowthRate = 0.10,
                MonthlyAgencySeatGrowthRate = 0.08,
                MonthlyEnterpriseAddOnGrowthRate = 0.05
            };

            var actuals = new List<RevenueActualSnapshot>
            {
                new()
                {
                    CapturedAtUtc = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
                    ActivePaidFreelancers = 100,
                    AgencySeats = 120,
                    EnterpriseAddOns = 2
                },
                new()
                {
                    CapturedAtUtc = new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc),
                    ActivePaidFreelancers = 110,
                    AgencySeats = 126,
                    EnterpriseAddOns = 3
                }
            };

            var service = new RevenuePlanningService();
            RevenuePlanningSnapshot snapshot = service.BuildSnapshot(plan, actuals);

            snapshot.Projections.Should().HaveCount(3);
            snapshot.Projections[0].HorizonMonths.Should().Be(3);
            snapshot.Projections[1].HorizonMonths.Should().Be(6);
            snapshot.Projections[2].HorizonMonths.Should().Be(12);
            snapshot.Projections[2].ProjectedArrUsd.Should().BeGreaterThan(snapshot.Projections[0].ProjectedArrUsd);
        }

        [Fact]
        public void BuildSnapshot_Should_Flag_Risks_For_Churn_Conversion_And_Regressions()
        {
            var plan = new ArrScenarioPlan
            {
                ScenarioType = ArrScenarioType.AgencyLed,
                PlannedActivePaidFreelancers = 200,
                PlannedAgencySeats = 400,
                PlannedEnterpriseAddOns = 10
            };

            var actuals = new List<RevenueActualSnapshot>
            {
                new()
                {
                    CapturedAtUtc = new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc),
                    ActivePaidFreelancers = 140,
                    AgencySeats = 180,
                    EnterpriseAddOns = 4
                }
            };

            var baseline = new PmfDashboardSnapshot
            {
                P95LatencyMs = 800,
                TerminologyViolationRate = 0.02
            };

            var latest = new PmfDashboardSnapshot
            {
                ChurnRate = 0.15,
                PilotToPaidConversion = 0.09,
                P95LatencyMs = 1100,
                TerminologyViolationRate = 0.03
            };

            var thresholds = new RevenueRiskThresholds
            {
                ChurnRateThreshold = 0.10,
                ConversionRateThreshold = 0.12,
                LatencyRegressionThresholdRatio = 0.20,
                QualityRegressionThresholdRatio = 0.20
            };

            var service = new RevenuePlanningService();
            RevenuePlanningSnapshot snapshot = service.BuildSnapshot(plan, actuals, latest, baseline, thresholds);

            snapshot.Risks.ChurnAboveThreshold.Should().BeTrue();
            snapshot.Risks.ConversionBelowThreshold.Should().BeTrue();
            snapshot.Risks.LatencyRegressionRisk.Should().BeTrue();
            snapshot.Risks.QualityRegressionRisk.Should().BeTrue();
            snapshot.Risks.Alerts.Should().HaveCount(4);
        }
    }
}
