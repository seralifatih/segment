using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class PricingEngineService : IPricingEngineService
    {
        private readonly PricingConfiguration _configuration;

        public PricingEngineService(PricingConfiguration? configuration = null)
        {
            _configuration = configuration ?? BuildDefaultConfiguration();
        }

        public PricingConfiguration GetConfiguration()
        {
            return _configuration;
        }

        public PlanEntitlements ResolveEntitlements(PricingPlan plan)
        {
            return plan switch
            {
                PricingPlan.LegalProIndividual => new PlanEntitlements
                {
                    GuardrailsLevel = "LegalStandard",
                    ConfidentialityModes = "Standard, High",
                    AdvancedGuardrails = false,
                    SharedGlossary = false,
                    AuditExport = false,
                    Analytics = true,
                    TeamAnalytics = false,
                    SlaTier = SlaTier.Standard
                },
                PricingPlan.LegalTeam => new PlanEntitlements
                {
                    GuardrailsLevel = "LegalAdvanced",
                    ConfidentialityModes = "Standard, High, TeamConfidential",
                    AdvancedGuardrails = true,
                    SharedGlossary = true,
                    AuditExport = true,
                    Analytics = true,
                    TeamAnalytics = true,
                    SlaTier = SlaTier.Business
                },
                PricingPlan.EnterpriseLegalAssurance => new PlanEntitlements
                {
                    GuardrailsLevel = "LegalAssuranceMax",
                    ConfidentialityModes = "Standard, High, TeamConfidential, AirGapReview",
                    AdvancedGuardrails = true,
                    SharedGlossary = true,
                    AuditExport = true,
                    Analytics = true,
                    TeamAnalytics = true,
                    SlaTier = SlaTier.Enterprise
                },
                _ => new PlanEntitlements()
            };
        }

        public ResolvedPricingPackage ResolvePackage(SubscriptionSelection selection)
        {
            var rule = ResolveRule(selection.Plan, selection.BillingInterval);
            int seats = NormalizeSeats(selection.Plan, selection.Seats, rule.MinimumSeats);

            decimal subtotal = rule.SeatBased
                ? rule.SeatPrice * seats
                : rule.BasePrice;

            decimal platformFee = 0;
            if (_configuration.PlatformFeeEnabled && selection.ApplyPlatformFee && rule.SupportsPlatformFee)
            {
                platformFee = selection.BillingInterval == BillingInterval.Monthly
                    ? _configuration.MonthlyPlatformFee
                    : _configuration.AnnualPlatformFee;
            }

            return new ResolvedPricingPackage
            {
                Plan = selection.Plan,
                BillingInterval = selection.BillingInterval,
                EffectiveSeats = seats,
                Subtotal = subtotal,
                PlatformFee = platformFee,
                Total = subtotal + platformFee,
                Entitlements = ResolveEntitlements(selection.Plan)
            };
        }

        public IReadOnlyList<PricingPlan> GetUpgradePaths(PricingPlan currentPlan)
        {
            int currentRank = Rank(currentPlan);
            return Enum.GetValues(typeof(PricingPlan))
                .Cast<PricingPlan>()
                .Where(x => Rank(x) > currentRank)
                .OrderBy(Rank)
                .ToList();
        }

        public PlanTransitionResult Upgrade(SubscriptionSelection currentSelection, PricingPlan targetPlan, int requestedSeats)
        {
            if (Rank(targetPlan) <= Rank(currentSelection.Plan))
            {
                return new PlanTransitionResult
                {
                    Allowed = false,
                    Reason = "Target plan is not an upgrade.",
                    UpdatedSelection = currentSelection,
                    UpgradePaths = GetUpgradePaths(currentSelection.Plan)
                };
            }

            var targetRule = ResolveRule(targetPlan, currentSelection.BillingInterval);
            int seats = NormalizeSeats(targetPlan, requestedSeats, targetRule.MinimumSeats);

            return new PlanTransitionResult
            {
                Allowed = true,
                Reason = "Upgrade applied.",
                UpdatedSelection = new SubscriptionSelection
                {
                    Plan = targetPlan,
                    BillingInterval = currentSelection.BillingInterval,
                    Seats = seats,
                    ApplyPlatformFee = currentSelection.ApplyPlatformFee
                },
                UpgradePaths = GetUpgradePaths(targetPlan)
            };
        }

        public PlanTransitionResult Downgrade(SubscriptionSelection currentSelection, PricingPlan targetPlan, int requestedSeats)
        {
            if (Rank(targetPlan) >= Rank(currentSelection.Plan))
            {
                return new PlanTransitionResult
                {
                    Allowed = false,
                    Reason = "Target plan is not a downgrade.",
                    UpdatedSelection = currentSelection,
                    UpgradePaths = GetUpgradePaths(currentSelection.Plan)
                };
            }

            var targetRule = ResolveRule(targetPlan, currentSelection.BillingInterval);
            int seats = NormalizeSeats(targetPlan, requestedSeats, targetRule.MinimumSeats);

            return new PlanTransitionResult
            {
                Allowed = true,
                Reason = "Downgrade applied.",
                UpdatedSelection = new SubscriptionSelection
                {
                    Plan = targetPlan,
                    BillingInterval = currentSelection.BillingInterval,
                    Seats = seats,
                    ApplyPlatformFee = currentSelection.ApplyPlatformFee
                },
                UpgradePaths = GetUpgradePaths(targetPlan)
            };
        }

        private static int Rank(PricingPlan plan)
        {
            return plan switch
            {
                PricingPlan.LegalProIndividual => 1,
                PricingPlan.LegalTeam => 2,
                PricingPlan.EnterpriseLegalAssurance => 3,
                _ => 0
            };
        }

        private PlanPriceRule ResolveRule(PricingPlan plan, BillingInterval interval)
        {
            var rule = _configuration.PriceRules.FirstOrDefault(x => x.Plan == plan && x.BillingInterval == interval);
            if (rule == null)
            {
                throw new InvalidOperationException($"No pricing rule found for {plan}/{interval}.");
            }

            return rule;
        }

        private static int NormalizeSeats(PricingPlan plan, int requestedSeats, int minSeats)
        {
            if (plan == PricingPlan.LegalProIndividual)
            {
                return 1;
            }

            int seats = Math.Max(requestedSeats, minSeats);
            return seats;
        }

        private static PricingConfiguration BuildDefaultConfiguration()
        {
            return new PricingConfiguration
            {
                PlatformFeeEnabled = true,
                MonthlyPlatformFee = 149m,
                AnnualPlatformFee = 149m * 12m * 0.9m,
                PriceRules = new List<PlanPriceRule>
                {
                    new()
                    {
                        Plan = PricingPlan.LegalProIndividual,
                        BillingInterval = BillingInterval.Monthly,
                        BasePrice = 79m,
                        SeatBased = false,
                        MinimumSeats = 1,
                        SupportsPlatformFee = false
                    },
                    new()
                    {
                        Plan = PricingPlan.LegalProIndividual,
                        BillingInterval = BillingInterval.Annual,
                        BasePrice = 790m,
                        SeatBased = false,
                        MinimumSeats = 1,
                        SupportsPlatformFee = false
                    },
                    new()
                    {
                        Plan = PricingPlan.LegalTeam,
                        BillingInterval = BillingInterval.Monthly,
                        SeatBased = true,
                        SeatPrice = 49m,
                        MinimumSeats = 3,
                        SupportsPlatformFee = true
                    },
                    new()
                    {
                        Plan = PricingPlan.LegalTeam,
                        BillingInterval = BillingInterval.Annual,
                        SeatBased = true,
                        SeatPrice = 490m,
                        MinimumSeats = 3,
                        SupportsPlatformFee = true
                    },
                    new()
                    {
                        Plan = PricingPlan.EnterpriseLegalAssurance,
                        BillingInterval = BillingInterval.Monthly,
                        BasePrice = 1499m,
                        SeatBased = false,
                        MinimumSeats = 1,
                        SupportsPlatformFee = true
                    },
                    new()
                    {
                        Plan = PricingPlan.EnterpriseLegalAssurance,
                        BillingInterval = BillingInterval.Annual,
                        BasePrice = 14990m,
                        SeatBased = false,
                        MinimumSeats = 1,
                        SupportsPlatformFee = true
                    }
                }
            };
        }
    }
}
