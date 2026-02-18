using System;
using System.Windows;
using System.Windows.Controls;
using Segment.App.Models;
using Segment.App.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Segment.App.Views
{
    public partial class WelcomeWindow : Window
    {
        private readonly IGtmConfigService _gtmConfigService;
        private readonly IOnboardingMetricsService _metricsService;
        private readonly IOnboardingFunnelService _funnelService;

        public WelcomeWindow()
        {
            InitializeComponent();

            _gtmConfigService = new GtmConfigService();
            _metricsService = new OnboardingMetricsService();
            _funnelService = new OnboardingFunnelService(
                _gtmConfigService,
                new OnboardingQualificationService(),
                _metricsService);

            RoleCombo.SelectedIndex = 0;
            ConfidentialityCombo.SelectedIndex = 0;
            RefreshPhaseBadge();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildProfile(out var profile, out var validationError))
            {
                DecisionText.Text = validationError;
                return;
            }

            var decision = _funnelService.Process(profile);
            DecisionText.Text = $"Decision: {decision.Outcome} | Score: {decision.Score}. {decision.Explanation}";

            switch (decision.Outcome)
            {
                case OnboardingOutcome.Accepted:
                    SettingsService.Current.IsFirstRun = false;
                    SettingsService.Current.IsLegalNicheUser = profile.DomainFocus.Contains("legal", StringComparison.OrdinalIgnoreCase);
                    SettingsService.Current.IsAgencyAccount = profile.Role == OnboardingRole.Agency;
                    SettingsService.Current.HasPilotContract = profile.Role != OnboardingRole.Freelancer;
                    SettingsService.Current.ConfidentialProjectLocalOnly = profile.ConfidentialityRequirementLevel == ConfidentialityRequirementLevel.Strict;
                    SettingsService.Current.ConfidentialityMode = profile.ConfidentialityRequirementLevel == ConfidentialityRequirementLevel.Strict
                        ? "LocalOnly"
                        : "Standard";
                    SettingsService.Save();

                    MessageBox.Show(
                        decision.Explanation,
                        "Welcome",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Close();
                    break;

                case OnboardingOutcome.Waitlist:
                    MessageBox.Show(
                        "You are on the waitlist. We will notify you as capacity opens up.",
                        "Waitlist",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                default:
                    MessageBox.Show(
                        decision.Explanation,
                        "Not Eligible Yet",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    break;
            }
        }

        private void RefreshPhaseBadge()
        {
            var phase = _gtmConfigService.GetActiveLaunchPhase();
            LaunchPhaseBadgeText.Text = $"Phase: {phase}";
        }

        private bool TryBuildProfile(out OnboardingProfile profile, out string error)
        {
            profile = new OnboardingProfile();
            error = string.Empty;

            if (RoleCombo.SelectedItem is not ComboBoxItem roleItem || roleItem.Content is not string roleText)
            {
                error = "Please select your role.";
                return false;
            }

            if (ConfidentialityCombo.SelectedItem is not ComboBoxItem confidentialityItem || confidentialityItem.Content is not string confidentialityText)
            {
                error = "Please select confidentiality requirement level.";
                return false;
            }

            if (!int.TryParse(WeeklyVolumeBox.Text?.Trim(), out int weeklyVolume) || weeklyVolume < 0)
            {
                error = "Weekly legal volume estimate must be a valid non-negative number.";
                return false;
            }

            string domainFocus = DomainFocusBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(domainFocus))
            {
                error = "Domain focus is required.";
                return false;
            }

            profile = new OnboardingProfile
            {
                Role = ParseRole(roleText),
                DomainFocus = domainFocus,
                WeeklyLegalVolumeEstimate = weeklyVolume,
                ConfidentialityRequirementLevel = ParseConfidentiality(confidentialityText),
                IntendsGlossaryUsage = GlossaryIntentBox.IsChecked == true
            };

            return true;
        }

        private static OnboardingRole ParseRole(string roleText)
        {
            return roleText switch
            {
                "Agency" => OnboardingRole.Agency,
                "Enterprise" => OnboardingRole.Enterprise,
                _ => OnboardingRole.Freelancer
            };
        }

        private static ConfidentialityRequirementLevel ParseConfidentiality(string confidentialityText)
        {
            return confidentialityText switch
            {
                "High" => ConfidentialityRequirementLevel.High,
                "Strict" => ConfidentialityRequirementLevel.Strict,
                _ => ConfidentialityRequirementLevel.Standard
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            (_gtmConfigService as IDisposable)?.Dispose();
            (_metricsService as IDisposable)?.Dispose();
            base.OnClosed(e);
        }
    }
}
