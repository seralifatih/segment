using FluentAssertions;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.Tests
{
    public class RoiBenchmarkCalculatorTests
    {
        [Fact]
        public void Calculate_Should_Return_Correct_Time_And_Violation_Deltas()
        {
            var session = new BenchmarkSession
            {
                Id = "session-1",
                WeekCaptures =
                {
                    new BenchmarkWeekCapture
                    {
                        WeekNumber = 1,
                        PeriodType = BenchmarkPeriodType.Baseline,
                        SegmentMetrics =
                        {
                            new BenchmarkSegmentMetric
                            {
                                Segment = CustomerSegment.FreelancerLegal,
                                SampleCount = 100,
                                AverageMinutesPerTask = 10,
                                TerminologyViolationCount = 20,
                                AcceptanceCount = 70,
                                EditCount = 30
                            }
                        }
                    },
                    new BenchmarkWeekCapture
                    {
                        WeekNumber = 2,
                        PeriodType = BenchmarkPeriodType.SegmentAssisted,
                        SegmentMetrics =
                        {
                            new BenchmarkSegmentMetric
                            {
                                Segment = CustomerSegment.FreelancerLegal,
                                SampleCount = 100,
                                AverageMinutesPerTask = 7,
                                TerminologyViolationCount = 10,
                                AcceptanceCount = 82,
                                EditCount = 18
                            }
                        }
                    }
                }
            };

            var calculator = new RoiBenchmarkCalculator();
            var report = calculator.Calculate(session);

            report.TimeSavedPercentage.Should().BeApproximately(30, 0.001);
            report.ViolationReductionPercentage.Should().BeApproximately(50, 0.001);
            report.AcceptanceRateDelta.Should().BeApproximately(0.12, 0.0001);
            report.EditRateDelta.Should().BeApproximately(-0.12, 0.0001);
        }

        [Fact]
        public void Calculate_Should_Assign_High_Confidence_For_Strong_Signal()
        {
            var session = new BenchmarkSession
            {
                Id = "session-2",
                WeekCaptures =
                {
                    new BenchmarkWeekCapture
                    {
                        WeekNumber = 1,
                        PeriodType = BenchmarkPeriodType.Baseline,
                        SegmentMetrics =
                        {
                            new BenchmarkSegmentMetric
                            {
                                Segment = CustomerSegment.AgencyLegal,
                                SampleCount = 150,
                                AverageMinutesPerTask = 12,
                                TerminologyViolationCount = 45,
                                AcceptanceCount = 80,
                                EditCount = 70
                            }
                        }
                    },
                    new BenchmarkWeekCapture
                    {
                        WeekNumber = 2,
                        PeriodType = BenchmarkPeriodType.SegmentAssisted,
                        SegmentMetrics =
                        {
                            new BenchmarkSegmentMetric
                            {
                                Segment = CustomerSegment.AgencyLegal,
                                SampleCount = 160,
                                AverageMinutesPerTask = 8,
                                TerminologyViolationCount = 20,
                                AcceptanceCount = 120,
                                EditCount = 40
                            }
                        }
                    }
                }
            };

            var calculator = new RoiBenchmarkCalculator();
            var report = calculator.Calculate(session);

            report.ConfidenceSummary.Should().Contain("High confidence");
        }
    }
}
