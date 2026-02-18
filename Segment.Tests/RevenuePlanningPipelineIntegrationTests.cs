using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class RevenuePlanningPipelineIntegrationTests
    {
        [Fact]
        public void Actuals_To_Forecast_Pipeline_Should_Produce_Trend_Projection_And_Conversion_Gap()
        {
            var plan = new ArrScenarioPlan
            {
                ScenarioType = ArrScenarioType.Mixed,
                TargetArrUsd = 1_000_000m,
                PlannedActivePaidFreelancers = 650,
                PlannedAgencySeats = 900,
                PlannedEnterpriseAddOns = 20,
                MonthlyFreelancerGrowthRate = 0.09,
                MonthlyAgencySeatGrowthRate = 0.11,
                MonthlyEnterpriseAddOnGrowthRate = 0.06,
                Pricing = new RevenuePricingAssumptions
                {
                    FreelancerMrrUsd = 79m,
                    AgencySeatMrrUsd = 49m,
                    EnterpriseAddOnMrrUsd = 1_499m
                }
            };

            var actuals = new List<RevenueActualSnapshot>
            {
                new() { CapturedAtUtc = new DateTime(2025, 11, 01, 0, 0, 0, DateTimeKind.Utc), ActivePaidFreelancers = 160, AgencySeats = 180, EnterpriseAddOns = 3 },
                new() { CapturedAtUtc = new DateTime(2025, 12, 01, 0, 0, 0, DateTimeKind.Utc), ActivePaidFreelancers = 175, AgencySeats = 205, EnterpriseAddOns = 3 },
                new() { CapturedAtUtc = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc), ActivePaidFreelancers = 192, AgencySeats = 230, EnterpriseAddOns = 4 },
                new() { CapturedAtUtc = new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc), ActivePaidFreelancers = 210, AgencySeats = 260, EnterpriseAddOns = 4 }
            };

            var baseline = new PmfDashboardSnapshot
            {
                P95LatencyMs = 900,
                TerminologyViolationRate = 0.02,
                ChurnRate = 0.07,
                PilotToPaidConversion = 0.22
            };

            var latest = new PmfDashboardSnapshot
            {
                P95LatencyMs = 980,
                TerminologyViolationRate = 0.021,
                ChurnRate = 0.08,
                PilotToPaidConversion = 0.24
            };

            var service = new RevenuePlanningService();
            RevenuePlanningSnapshot snapshot = service.BuildSnapshot(plan, actuals, latest, baseline);

            snapshot.Trend.Should().HaveCount(4);
            snapshot.Trend.Should().BeInAscendingOrder(x => x.CapturedAtUtc);
            snapshot.ActualActivePaidFreelancers.Should().Be(210);
            snapshot.ActualAgencySeats.Should().Be(260);
            snapshot.ActualEnterpriseAddOns.Should().Be(4);

            snapshot.Projections.Select(x => x.HorizonMonths).Should().Equal(3, 6, 12);
            snapshot.Projections[0].ProjectedArrUsd.Should().BeGreaterThan(snapshot.ActualArrUsd);
            snapshot.Projections[2].ProjectedArrUsd.Should().BeGreaterThan(snapshot.Projections[0].ProjectedArrUsd);

            RevenueProjectionPoint twelveMonth = snapshot.Projections.Single(x => x.HorizonMonths == 12);
            twelveMonth.GapToTargetArrUsd.Should().BeGreaterThanOrEqualTo(0m);
            twelveMonth.RequiredConversions.RequiredPaidFreelancers.Should().BeGreaterThanOrEqualTo(0);
            twelveMonth.RequiredConversions.RequiredAgencySeats.Should().BeGreaterThanOrEqualTo(0);
            twelveMonth.RequiredConversions.RequiredEnterpriseAddOns.Should().BeGreaterThanOrEqualTo(0);

            snapshot.Risks.ChurnAboveThreshold.Should().BeFalse();
            snapshot.Risks.ConversionBelowThreshold.Should().BeFalse();
            snapshot.Risks.LatencyRegressionRisk.Should().BeFalse();
            snapshot.Risks.QualityRegressionRisk.Should().BeFalse();
        }
    }
}
