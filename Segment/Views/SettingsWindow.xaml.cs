using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Segment.App.Models;
using Segment.App.Services;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Segment.App.Views
{
    public partial class SettingsWindow : Window
    {
        private const string RunRegistryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "SegmentApp";
        private readonly IGtmConfigService _gtmConfigService;
        private readonly LaunchPhaseGateService _phaseGateService;
        private readonly IPricingEngineService _pricingEngineService;
        private readonly ReferralService _referralService;
        private readonly GlossaryPackSharingService _glossaryPackSharingService;
        private readonly IPilotWorkspaceService _pilotWorkspaceService;
        private readonly IDemoDatasetService _demoDatasetService;
        private readonly IAccountMetadataService _accountMetadataService;
        private readonly IGlossaryQualityReportService _glossaryQualityReportService;
        private readonly ICoBrandedExportService _coBrandedExportService;
        private readonly IPmfDashboardService _pmfDashboardService;
        private readonly IDecisionGateEvaluator _decisionGateEvaluator;
        private readonly IPmfSnapshotExportService _pmfSnapshotExportService;
        private readonly INicheTelemetryService _nicheTelemetryService;
        private readonly IPlanEntitlementService _planEntitlementService;
        private readonly ComplianceAuditService _complianceAuditService;
        private readonly INicheTemplateService _nicheTemplateService;

        public SettingsWindow()
        {
            InitializeComponent();
            _gtmConfigService = new GtmConfigService();
            _phaseGateService = new LaunchPhaseGateService(_gtmConfigService);
            _pricingEngineService = new PricingEngineService();
            _referralService = new ReferralService();
            _glossaryPackSharingService = new GlossaryPackSharingService(_referralService);
            _pilotWorkspaceService = new PilotWorkspaceService();
            _demoDatasetService = new DemoDatasetService();
            _accountMetadataService = new AccountMetadataService();
            _glossaryQualityReportService = new GlossaryQualityReportService();
            _coBrandedExportService = new CoBrandedExportService();
            _pmfDashboardService = new PmfDashboardService();
            _decisionGateEvaluator = new DecisionGateEvaluator(_gtmConfigService);
            _pmfSnapshotExportService = new PmfSnapshotExportService();
            _nicheTelemetryService = new NicheTelemetryService();
            _planEntitlementService = new PlanEntitlementService(_pricingEngineService);
            _complianceAuditService = ComplianceAuditService.Default;
            _nicheTemplateService = new NicheTemplateService();
            LoadSettings();

            this.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
        }

        private void LoadSettings()
        {
            LanguageCombo.Text = SettingsService.Current.TargetLanguage;

            switch (SettingsService.Current.AiProvider)
            {
                case "Ollama": ProviderCombo.SelectedIndex = 1; break;
                case "Custom": ProviderCombo.SelectedIndex = 2; break;
                default: ProviderCombo.SelectedIndex = 0; break;
            }

            GoogleApiKeyBox.Password = SettingsService.Current.GoogleApiKey;
            GoogleModelBox.Text = SettingsService.Current.GoogleModel;

            OllamaUrlBox.Text = SettingsService.Current.OllamaUrl;
            OllamaModelBox.Text = SettingsService.Current.OllamaModel;

            CustomUrlBox.Text = SettingsService.Current.CustomBaseUrl;
            CustomApiKeyBox.Password = SettingsService.Current.CustomApiKey;
            CustomModelBox.Text = SettingsService.Current.CustomModel;

            StartupBox.IsChecked = IsStartupEnabled();
            ShowPanelHotkeyBox.Text = SettingsService.Current.ShowPanelHotkey;
            TranslateInPlaceHotkeyBox.Text = SettingsService.Current.TranslateSelectionInPlaceHotkey;
            RefreshHotkeyConflictState();

            PreviewAdminCheck.IsChecked = SettingsService.Current.IsAdminUser;
            PreviewInvitedCheck.IsChecked = SettingsService.Current.IsInvitedUser;
            PreviewLegalCheck.IsChecked = SettingsService.Current.IsLegalNicheUser;
            PreviewAgencyCheck.IsChecked = SettingsService.Current.IsAgencyAccount;
            PreviewPilotContractCheck.IsChecked = SettingsService.Current.HasPilotContract;

            LaunchPhaseCombo.ItemsSource = Enum.GetValues(typeof(LaunchPhase)).Cast<LaunchPhase>().ToList();
            LaunchPhaseCombo.SelectedItem = _gtmConfigService.GetActiveLaunchPhase();
            RefreshPhaseBadge();
            RefreshPhasePreview();

            PricingPlanCombo.ItemsSource = Enum.GetValues(typeof(PricingPlan)).Cast<PricingPlan>().ToList();
            BillingIntervalCombo.ItemsSource = Enum.GetValues(typeof(BillingInterval)).Cast<BillingInterval>().ToList();
            PricingPlanCombo.SelectedItem = ParsePlan(SettingsService.Current.ActivePricingPlan);
            BillingIntervalCombo.SelectedItem = ParseBilling(SettingsService.Current.ActiveBillingInterval);
            SeatCountBox.Text = SettingsService.Current.ActiveSeatCount.ToString();
            PlatformFeeBox.IsChecked = SettingsService.Current.ApplyPlatformFee;
            RefreshPricingPreview();

            PilotWorkspaceModeBox.IsChecked = SettingsService.Current.PilotWorkspaceModeEnabled;
            DemoDatasetModeBox.IsChecked = SettingsService.Current.DemoDatasetModeEnabled;
            AccountIdBox.Text = SettingsService.Current.AccountId;
            AccountNameBox.Text = SettingsService.Current.AccountDisplayName;
            PartnerTagsBox.Text = SettingsService.Current.PartnerTagsCsv;
            PilotAgencyNameBox.Text = string.IsNullOrWhiteSpace(SettingsService.Current.AccountDisplayName)
                ? "Agency Pilot Workspace"
                : $"{SettingsService.Current.AccountDisplayName} Pilot";
            PilotSeatLimitBox.Text = Math.Max(1, SettingsService.Current.ActiveSeatCount).ToString();
            PilotWorkspaceStatusText.Text = string.IsNullOrWhiteSpace(SettingsService.Current.ActivePilotWorkspaceId)
                ? "Pilot workspace mode is available for agency pilots."
                : $"Active pilot workspace: {SettingsService.Current.ActivePilotWorkspaceId}";

            ConfidentialProjectModeBox.IsChecked = SettingsService.Current.ConfidentialProjectLocalOnly;
            ConfidentialityModeCombo.SelectedItem = FindConfidentialityModeItem(
                string.IsNullOrWhiteSpace(SettingsService.Current.ConfidentialityMode)
                    ? "Standard"
                    : SettingsService.Current.ConfidentialityMode);
            if (SettingsService.Current.ConfidentialProjectLocalOnly)
            {
                ConfidentialityModeCombo.SelectedItem = FindConfidentialityModeItem("LocalOnly");
            }
            GuardrailOverrideBox.IsChecked = SettingsService.Current.AllowGuardrailOverrides;
            QaStrictModeBox.IsChecked = SettingsService.Current.QaStrictMode;
            MinimizeDiagnosticLoggingBox.IsChecked = SettingsService.Current.MinimizeDiagnosticLogging;
            EnforceApprovedProvidersBox.IsChecked = SettingsService.Current.EnforceApprovedProviders;
            ApprovedProvidersBox.Text = SettingsService.Current.ApprovedProvidersCsv;
            PreferLocalProcessingPathBox.IsChecked = SettingsService.Current.PreferLocalProcessingPath;
            RetentionPolicySummaryBox.Text = SettingsService.Current.RetentionPolicySummary;
            TelemetryUsageMetricsBox.IsChecked = SettingsService.Current.TelemetryUsageMetricsConsent;
            TelemetryCrashDiagnosticsBox.IsChecked = SettingsService.Current.TelemetryCrashDiagnosticsConsent;
            TelemetryModelOutputBox.IsChecked = SettingsService.Current.TelemetryModelOutputConsent;
            TelemetryConsentLockBox.IsChecked = SettingsService.Current.TelemetryConsentLockEnabled;
            RefreshDataHandlingDisclosure();
            RefreshTelemetryLockState();
            RefreshPmfDashboardText();
            LoadNicheTemplates();
            RefreshEntitlementInspector();

            UpdatePanelVisibility();
        }

        private void LoadNicheTemplates()
        {
            var templates = _nicheTemplateService.GetBuiltInTemplates();
            NicheTemplateCombo.ItemsSource = templates;
            NicheTemplateCombo.SelectedItem = templates.FirstOrDefault(x =>
                string.Equals(x.Domain.ToString(), SettingsService.Current.ActiveDomain, StringComparison.OrdinalIgnoreCase))
                ?? templates.FirstOrDefault();
            NicheProjectNameBox.Text = GlossaryService.CurrentProfile.Name;
        }

        private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePanelVisibility();
            RefreshDataHandlingDisclosure();
        }

        private void UpdatePanelVisibility()
        {
            if (GooglePanel == null || OllamaPanel == null || CustomPanel == null) return;

            GooglePanel.Visibility = Visibility.Collapsed;
            OllamaPanel.Visibility = Visibility.Collapsed;
            CustomPanel.Visibility = Visibility.Collapsed;

            if (ProviderCombo.SelectedIndex == 0) GooglePanel.Visibility = Visibility.Visible;
            else if (ProviderCombo.SelectedIndex == 1) OllamaPanel.Visibility = Visibility.Visible;
            else if (ProviderCombo.SelectedIndex == 2) CustomPanel.Visibility = Visibility.Visible;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateHotkeys())
            {
                return;
            }

            SettingsService.Current.TargetLanguage = LanguageCombo.Text;

            if (ProviderCombo.SelectedIndex == 0) SettingsService.Current.AiProvider = "Google";
            else if (ProviderCombo.SelectedIndex == 1) SettingsService.Current.AiProvider = "Ollama";
            else SettingsService.Current.AiProvider = "Custom";

            SettingsService.Current.GoogleApiKey = GoogleApiKeyBox.Password;
            SettingsService.Current.GoogleModel = GoogleModelBox.Text;

            SettingsService.Current.OllamaUrl = OllamaUrlBox.Text;
            SettingsService.Current.OllamaModel = OllamaModelBox.Text;

            SettingsService.Current.CustomBaseUrl = CustomUrlBox.Text;
            SettingsService.Current.CustomApiKey = CustomApiKeyBox.Password;
            SettingsService.Current.CustomModel = CustomModelBox.Text;
            SettingsService.Current.ShowPanelHotkey = ShowPanelHotkeyBox.Text?.Trim() ?? "Ctrl+Space";
            SettingsService.Current.TranslateSelectionInPlaceHotkey = TranslateInPlaceHotkeyBox.Text?.Trim() ?? "Ctrl+Shift+Space";

            SettingsService.Current.IsAdminUser = PreviewAdminCheck.IsChecked == true;
            SettingsService.Current.IsInvitedUser = PreviewInvitedCheck.IsChecked == true;
            SettingsService.Current.IsLegalNicheUser = PreviewLegalCheck.IsChecked == true;
            SettingsService.Current.IsAgencyAccount = PreviewAgencyCheck.IsChecked == true;
            SettingsService.Current.HasPilotContract = PreviewPilotContractCheck.IsChecked == true;
            SettingsService.Current.ActivePricingPlan = (PricingPlanCombo.SelectedItem as PricingPlan?)?.ToString() ?? PricingPlan.LegalProIndividual.ToString();
            SettingsService.Current.ActiveBillingInterval = (BillingIntervalCombo.SelectedItem as BillingInterval?)?.ToString() ?? BillingInterval.Monthly.ToString();
            SettingsService.Current.ActiveSeatCount = ParseSeatCount();
            SettingsService.Current.ApplyPlatformFee = PlatformFeeBox.IsChecked == true;
            SettingsService.Current.PilotWorkspaceModeEnabled = PilotWorkspaceModeBox.IsChecked == true;
            SettingsService.Current.DemoDatasetModeEnabled = DemoDatasetModeBox.IsChecked == true;
            SettingsService.Current.AccountId = string.IsNullOrWhiteSpace(AccountIdBox.Text) ? "account-local" : AccountIdBox.Text.Trim();
            SettingsService.Current.AccountDisplayName = string.IsNullOrWhiteSpace(AccountNameBox.Text) ? SettingsService.Current.AccountId : AccountNameBox.Text.Trim();
            SettingsService.Current.PartnerTagsCsv = PartnerTagsBox.Text?.Trim() ?? "";
            SettingsService.Current.ConfidentialProjectLocalOnly = ConfidentialProjectModeBox.IsChecked == true;
            SettingsService.Current.ConfidentialityMode =
                (ConfidentialityModeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Standard";
            if (string.Equals(SettingsService.Current.ConfidentialityMode, "LocalOnly", StringComparison.OrdinalIgnoreCase))
            {
                SettingsService.Current.ConfidentialProjectLocalOnly = true;
            }
            SettingsService.Current.AllowGuardrailOverrides = GuardrailOverrideBox.IsChecked == true;
            SettingsService.Current.QaStrictMode = QaStrictModeBox.IsChecked == true;
            SettingsService.Current.MinimizeDiagnosticLogging = MinimizeDiagnosticLoggingBox.IsChecked == true;
            SettingsService.Current.EnforceApprovedProviders = EnforceApprovedProvidersBox.IsChecked == true;
            SettingsService.Current.ApprovedProvidersCsv = string.IsNullOrWhiteSpace(ApprovedProvidersBox.Text)
                ? "Google,Ollama,Custom"
                : ApprovedProvidersBox.Text.Trim();
            SettingsService.Current.PreferLocalProcessingPath = PreferLocalProcessingPathBox.IsChecked == true;
            SettingsService.Current.RetentionPolicySummary = string.IsNullOrWhiteSpace(RetentionPolicySummaryBox.Text)
                ? "Glossary and audit records are retained locally. Cloud telemetry is opt-in by category."
                : RetentionPolicySummaryBox.Text.Trim();

            string currentAccountId = SettingsService.Current.AccountId;
            bool lockRequested = TelemetryConsentLockBox.IsChecked == true;
            bool lockedForDifferentAccount = SettingsService.Current.TelemetryConsentLockEnabled
                && !string.IsNullOrWhiteSpace(SettingsService.Current.TelemetryConsentLockedByAccountId)
                && !string.Equals(SettingsService.Current.TelemetryConsentLockedByAccountId, currentAccountId, StringComparison.OrdinalIgnoreCase);

            if (!lockedForDifferentAccount)
            {
                SettingsService.Current.TelemetryUsageMetricsConsent = TelemetryUsageMetricsBox.IsChecked == true;
                SettingsService.Current.TelemetryCrashDiagnosticsConsent = TelemetryCrashDiagnosticsBox.IsChecked == true;
                SettingsService.Current.TelemetryModelOutputConsent = TelemetryModelOutputBox.IsChecked == true;
                SettingsService.Current.TelemetryConsentLockEnabled = lockRequested;

                if (lockRequested && string.IsNullOrWhiteSpace(SettingsService.Current.TelemetryConsentLockedByAccountId))
                {
                    SettingsService.Current.TelemetryConsentLockedByAccountId = currentAccountId;
                }
                else if (!lockRequested)
                {
                    SettingsService.Current.TelemetryConsentLockedByAccountId = string.Empty;
                }
            }

            var tags = ParsePartnerTags(SettingsService.Current.PartnerTagsCsv);
            _accountMetadataService.SetPartnerTags(SettingsService.Current.AccountId, tags, SettingsService.Current.AccountDisplayName);

            SettingsService.Save();
            if (System.Windows.Application.Current is Segment.App.App app)
            {
                app.RefreshHotkeys();
            }
            SetStartup(StartupBox.IsChecked == true);

            Close();
        }

        private void ImportTmx_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "TMX files (*.tmx)|*.tmx|All files (*.*)|*.*",
                Title = "Import TMX"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var terms = TmxImportService.Import(dialog.FileName, SettingsService.Current.TargetLanguage);
                int inserted = GlossaryService.AddTerms(terms, isGlobal: true);
                MessageBox.Show($"Imported {inserted} terms into Global profile.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"TMX import failed: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LaunchPhaseCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshPhasePreview();
        }

        private void PreviewContext_Changed(object sender, RoutedEventArgs e)
        {
            RefreshPhasePreview();
        }

        private void ApplyLaunchPhase_Click(object sender, RoutedEventArgs e)
        {
            if (LaunchPhaseCombo.SelectedItem is not LaunchPhase selectedPhase)
            {
                return;
            }

            var config = _gtmConfigService.LoadConfig();
            config.ActiveLaunchPhase = selectedPhase;
            _gtmConfigService.SaveConfig(config);

            RefreshPhaseBadge();
            RefreshPhasePreview();

            MessageBox.Show(
                $"Active launch phase changed to {selectedPhase}.",
                "Launch Phase Updated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RefreshPhaseBadge()
        {
            LaunchPhaseBadgeText.Text = $"Phase: {_gtmConfigService.GetActiveLaunchPhase()}";
        }

        private void RefreshPhasePreview()
        {
            if (LaunchPhaseCombo.SelectedItem is not LaunchPhase selectedPhase)
            {
                return;
            }

            var userContext = new LaunchUserContext
            {
                IsAdminUser = PreviewAdminCheck.IsChecked == true,
                IsInvitedUser = PreviewInvitedCheck.IsChecked == true,
                IsLegalNicheUser = PreviewLegalCheck.IsChecked == true,
                IsAgencyAccount = PreviewAgencyCheck.IsChecked == true,
                HasPilotContract = PreviewPilotContractCheck.IsChecked == true
            };

            LaunchPhasePreviewText.Text = _phaseGateService.BuildPhasePreview(selectedPhase, userContext);
        }

        private void PricingSelection_Changed(object sender, RoutedEventArgs e)
        {
            RefreshPricingPreview();
        }

        private void PreviewPlan_Click(object sender, RoutedEventArgs e)
        {
            RefreshPricingPreview();
        }

        private void UpgradePlan_Click(object sender, RoutedEventArgs e)
        {
            var current = BuildCurrentSelectionFromSettings();
            var target = BuildCurrentSelectionFromUi();
            var result = _pricingEngineService.Upgrade(current, target.Plan, target.Seats);
            ApplyTransitionResult(result);
        }

        private void DowngradePlan_Click(object sender, RoutedEventArgs e)
        {
            var current = BuildCurrentSelectionFromSettings();
            var target = BuildCurrentSelectionFromUi();
            var result = _pricingEngineService.Downgrade(current, target.Plan, target.Seats);
            ApplyTransitionResult(result);
        }

        private void ApplyTransitionResult(PlanTransitionResult result)
        {
            if (!result.Allowed)
            {
                PricingDetailsText.Text = $"Transition blocked: {result.Reason}";
                return;
            }

            PricingPlanCombo.SelectedItem = result.UpdatedSelection.Plan;
            BillingIntervalCombo.SelectedItem = result.UpdatedSelection.BillingInterval;
            SeatCountBox.Text = result.UpdatedSelection.Seats.ToString();
            PlatformFeeBox.IsChecked = result.UpdatedSelection.ApplyPlatformFee;

            SettingsService.Current.ActivePricingPlan = result.UpdatedSelection.Plan.ToString();
            SettingsService.Current.ActiveBillingInterval = result.UpdatedSelection.BillingInterval.ToString();
            SettingsService.Current.ActiveSeatCount = result.UpdatedSelection.Seats;
            SettingsService.Current.ApplyPlatformFee = result.UpdatedSelection.ApplyPlatformFee;
            SettingsService.Save();

            RefreshPricingPreview();
        }

        private void RefreshPricingPreview()
        {
            var selection = BuildCurrentSelectionFromUi();
            var resolved = _pricingEngineService.ResolvePackage(selection);
            var entitlements = resolved.Entitlements;
            PricingDetailsText.Text =
                $"Total: {resolved.Total:C} ({resolved.BillingInterval}) | Seats: {resolved.EffectiveSeats}\n" +
                $"Guardrails: {entitlements.GuardrailsLevel} | Confidentiality: {entitlements.ConfidentialityModes}\n" +
                $"Shared Glossary: {(entitlements.SharedGlossary ? "Yes" : "No")} | Audit Export: {(entitlements.AuditExport ? "Yes" : "No")} | Analytics: {(entitlements.Analytics ? "Yes" : "No")} | SLA: {entitlements.SlaTier}";
            RefreshEntitlementInspector();
        }

        private void InspectEntitlements_Click(object sender, RoutedEventArgs e)
        {
            RefreshEntitlementInspector();
        }

        private void RefreshEntitlementInspector()
        {
            if (EntitlementStatusText == null)
            {
                return;
            }

            EntitlementStatusText.Text = _planEntitlementService.BuildEntitlementSummary();
        }

        private SubscriptionSelection BuildCurrentSelectionFromUi()
        {
            return new SubscriptionSelection
            {
                Plan = (PricingPlanCombo.SelectedItem as PricingPlan?) ?? PricingPlan.LegalProIndividual,
                BillingInterval = (BillingIntervalCombo.SelectedItem as BillingInterval?) ?? BillingInterval.Monthly,
                Seats = ParseSeatCount(),
                ApplyPlatformFee = PlatformFeeBox.IsChecked == true
            };
        }

        private SubscriptionSelection BuildCurrentSelectionFromSettings()
        {
            return new SubscriptionSelection
            {
                Plan = ParsePlan(SettingsService.Current.ActivePricingPlan),
                BillingInterval = ParseBilling(SettingsService.Current.ActiveBillingInterval),
                Seats = Math.Max(1, SettingsService.Current.ActiveSeatCount),
                ApplyPlatformFee = SettingsService.Current.ApplyPlatformFee
            };
        }

        private int ParseSeatCount()
        {
            return int.TryParse(SeatCountBox.Text?.Trim(), out int seats) ? Math.Max(1, seats) : 1;
        }

        private static PricingPlan ParsePlan(string value)
        {
            return Enum.TryParse<PricingPlan>(value, out var parsed) ? parsed : PricingPlan.LegalProIndividual;
        }

        private static BillingInterval ParseBilling(string value)
        {
            return Enum.TryParse<BillingInterval>(value, out var parsed) ? parsed : BillingInterval.Monthly;
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (exePath != null && exePath.EndsWith(".dll"))
                    {
                        exePath = exePath.Replace(".dll", ".exe");
                    }

                    if (exePath != null)
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not change startup settings: {ex.Message}");
            }
        }

        private void SetupPilotWorkspace_Click(object sender, RoutedEventArgs e)
        {
            EntitlementCheckResult sharedGlossaryGate = _planEntitlementService.CheckFeature(EntitlementFeature.SharedGlossaryWorkspace);
            if (!sharedGlossaryGate.Allowed)
            {
                PilotWorkspaceStatusText.Text = sharedGlossaryGate.Message;
                return;
            }

            try
            {
                string agencyName = string.IsNullOrWhiteSpace(PilotAgencyNameBox.Text) ? "Agency Pilot" : PilotAgencyNameBox.Text.Trim();
                string ownerId = string.IsNullOrWhiteSpace(AccountIdBox.Text) ? "account-local" : AccountIdBox.Text.Trim();
                int seatLimit = int.TryParse(PilotSeatLimitBox.Text?.Trim(), out int parsedSeatLimit) ? Math.Max(1, parsedSeatLimit) : 5;
                List<string> tags = ParsePartnerTags(PartnerTagsBox.Text);

                var workspace = _pilotWorkspaceService.CreateWorkspace(agencyName, ownerId, seatLimit, tags);
                int glossarySeedCount = _pilotWorkspaceService.BootstrapSharedGlossary(workspace.Id);

                SettingsService.Current.ActivePilotWorkspaceId = workspace.Id;
                SettingsService.Current.PilotWorkspaceModeEnabled = true;
                SettingsService.Save();

                var dashboard = _pilotWorkspaceService.GetKpiDashboard(workspace.Id);
                PilotWorkspaceStatusText.Text =
                    $"Workspace {workspace.Id} ready. Glossary terms seeded: {glossarySeedCount}. " +
                    $"Seats: {dashboard.AcceptedSeatCount}/{dashboard.SeatLimit}.";
            }
            catch (Exception ex)
            {
                PilotWorkspaceStatusText.Text = $"Workspace setup failed: {ex.Message}";
            }
        }

        private void InvitePilotSeat_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SettingsService.Current.ActivePilotWorkspaceId))
            {
                PilotWorkspaceStatusText.Text = "Create a pilot workspace before inviting seats.";
                return;
            }

            if (string.IsNullOrWhiteSpace(PilotInviteEmailBox.Text))
            {
                PilotWorkspaceStatusText.Text = "Invite email is required.";
                return;
            }

            try
            {
                var invite = _pilotWorkspaceService.InviteSeat(SettingsService.Current.ActivePilotWorkspaceId, PilotInviteEmailBox.Text.Trim());
                var dashboard = _pilotWorkspaceService.GetKpiDashboard(SettingsService.Current.ActivePilotWorkspaceId);
                PilotWorkspaceStatusText.Text =
                    $"Invite queued for {invite.Email}. Invited: {dashboard.InvitedSeatCount}, Accepted: {dashboard.AcceptedSeatCount}.";
            }
            catch (Exception ex)
            {
                PilotWorkspaceStatusText.Text = $"Seat invite failed: {ex.Message}";
            }
        }

        private void PreviewDemoReplay_Click(object sender, RoutedEventArgs e)
        {
            int seed = 42;
            string id = SettingsService.Current.ActivePilotWorkspaceId ?? "";
            foreach (char ch in id)
            {
                seed = ((seed * 31) + ch) & 0x7fffffff;
            }

            var frames = _demoDatasetService.BuildDeterministicReplay(seed, 3);
            string preview = string.Join(" | ", frames.Select(x => $"{x.StepNumber}:{x.ClauseId}"));
            PilotWorkspaceStatusText.Text = $"Demo replay sequence: {preview}";
        }

        private void ExportPilotSummaryTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SettingsService.Current.ActivePilotWorkspaceId))
            {
                PilotWorkspaceStatusText.Text = "Create a pilot workspace before exporting pilot summary.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv|PDF file (*.pdf)|*.pdf",
                Title = "Export co-branded pilot outcome summary",
                FileName = "co-branded-pilot-outcome-summary.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var dashboard = _pilotWorkspaceService.GetKpiDashboard(SettingsService.Current.ActivePilotWorkspaceId);
                var synthetic = new PilotRoiReport
                {
                    SessionId = SettingsService.Current.ActivePilotWorkspaceId,
                    TimeSavedPercentage = Math.Max(0, (1 - (dashboard.AverageP95LatencyMs / 1500d)) * 100),
                    ViolationReductionPercentage = Math.Max(0, (0.05 - dashboard.AverageTermViolationRate) * 1000),
                    ConfidenceSummary = dashboard.TotalSamples >= 3 ? "High confidence from pilot KPI trend." : "Medium confidence, collect more samples."
                };

                var options = BuildExportOptions();
                if (dialog.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    _coBrandedExportService.ExportPilotOutcomeSummaryPdf(synthetic, options, dialog.FileName);
                }
                else
                {
                    _coBrandedExportService.ExportPilotOutcomeSummaryCsv(synthetic, options, dialog.FileName);
                }

                PilotWorkspaceStatusText.Text = $"Pilot outcome template exported: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                PilotWorkspaceStatusText.Text = $"Pilot summary export failed: {ex.Message}";
            }
        }

        private void ExportGlossaryQualityTemplate_Click(object sender, RoutedEventArgs e)
        {
            string workspaceId = string.IsNullOrWhiteSpace(SettingsService.Current.ActivePilotWorkspaceId)
                ? "workspace-n/a"
                : SettingsService.Current.ActivePilotWorkspaceId;

            var dialog = new SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv|PDF file (*.pdf)|*.pdf",
                Title = "Export co-branded glossary quality report",
                FileName = "co-branded-glossary-quality-report.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var qualityReport = _glossaryQualityReportService.BuildReport(workspaceId);
                var options = BuildExportOptions();

                if (dialog.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    _coBrandedExportService.ExportGlossaryQualityReportPdf(qualityReport, options, dialog.FileName);
                }
                else
                {
                    _coBrandedExportService.ExportGlossaryQualityReportCsv(qualityReport, options, dialog.FileName);
                }

                PilotWorkspaceStatusText.Text = $"Glossary quality template exported: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                PilotWorkspaceStatusText.Text = $"Glossary quality export failed: {ex.Message}";
            }
        }

        private CoBrandedExportOptions BuildExportOptions()
        {
            return new CoBrandedExportOptions
            {
                PartnerName = AccountNameBox.Text?.Trim() ?? "",
                PartnerTagline = PartnerTagsBox.Text?.Trim() ?? "",
                WorkspaceName = PilotAgencyNameBox.Text?.Trim() ?? "",
                GeneratedBy = AccountIdBox.Text?.Trim() ?? ""
            };
        }

        private static List<string> ParsePartnerTags(string? rawTags)
        {
            return (rawTags ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void RefreshPmfDashboard_Click(object sender, RoutedEventArgs e)
        {
            EntitlementCheckResult teamAnalyticsGate = _planEntitlementService.CheckFeature(EntitlementFeature.TeamAnalytics);
            if (!teamAnalyticsGate.Allowed)
            {
                PmfDashboardText.Text = teamAnalyticsGate.Message;
                return;
            }

            RefreshPmfDashboardText();
        }

        private void ExportPmfSnapshot_Click(object sender, RoutedEventArgs e)
        {
            EntitlementCheckResult teamAnalyticsGate = _planEntitlementService.CheckFeature(EntitlementFeature.TeamAnalytics);
            if (!teamAnalyticsGate.Allowed)
            {
                PmfDashboardText.Text = teamAnalyticsGate.Message;
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv|PDF file (*.pdf)|*.pdf",
                Title = "Export PMF weekly snapshot",
                FileName = "pmf-weekly-snapshot.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var snapshot = GetCurrentWeekSnapshot();
                var decision = _decisionGateEvaluator.Evaluate(_gtmConfigService.GetActiveLaunchPhase(), snapshot);

                if (dialog.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    _pmfSnapshotExportService.ExportWeeklyPdf(snapshot, decision, dialog.FileName);
                }
                else
                {
                    _pmfSnapshotExportService.ExportWeeklyCsv(snapshot, decision, dialog.FileName);
                }

                PmfDashboardText.Text = $"PMF snapshot exported: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                PmfDashboardText.Text = $"PMF snapshot export failed: {ex.Message}";
            }
        }

        private void ExportNicheTelemetryMetrics_Click(object sender, RoutedEventArgs e)
        {
            EntitlementCheckResult teamAnalyticsGate = _planEntitlementService.CheckFeature(EntitlementFeature.TeamAnalytics);
            if (!teamAnalyticsGate.Allowed)
            {
                PmfDashboardText.Text = teamAnalyticsGate.Message;
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv",
                Title = "Export niche PMF telemetry metrics",
                FileName = "niche-pmf-telemetry-metrics.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                DateTime end = DateTime.UtcNow;
                DateTime start = end.AddDays(-7);
                _nicheTelemetryService.ExportMetricsCsv(start, end, dialog.FileName);
                PmfDashboardText.Text = $"Niche telemetry metrics exported: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                PmfDashboardText.Text = $"Niche telemetry export failed: {ex.Message}";
            }
        }

        private void RefreshPmfDashboardText()
        {
            try
            {
                var snapshot = GetCurrentWeekSnapshot();
                var phase = _gtmConfigService.GetActiveLaunchPhase();
                var decision = _decisionGateEvaluator.Evaluate(phase, snapshot);
                PmfDashboardText.Text =
                    $"DAU/WAU: {snapshot.Dau}/{snapshot.Wau} | Segments/day: {snapshot.SegmentsPerDay:F1} | " +
                    $"Week-4 retention: {snapshot.RetentionWeek4:P1} | Glossary reuse: {snapshot.GlossaryReuseRate:P1}\n" +
                    $"Violation rate: {snapshot.TerminologyViolationRate:P2} | Latency p50/p95: {snapshot.P50LatencyMs:F0}/{snapshot.P95LatencyMs:F0} ms | " +
                    $"Pilot->Paid: {snapshot.PilotToPaidConversion:P1} | Churn: {snapshot.ChurnRate:P1}\n" +
                    $"Gate ({phase}): {decision.Recommendation} ({decision.PassedCount} pass / {decision.FailedCount} fail)";
            }
            catch (Exception ex)
            {
                PmfDashboardText.Text = $"PMF dashboard unavailable: {ex.Message}";
            }
        }

        private void RefreshDataHandlingDisclosure()
        {
            try
            {
                DataHandlingDisclosure disclosure = TranslationService.BuildDataHandlingDisclosure(SettingsService.Current);
                DataHandlingDisclosureText.Text =
                    $"Active mode: {disclosure.ActiveMode}\n" +
                    $"Provider route: {disclosure.ProviderRoute}\n" +
                    $"Retention policy: {disclosure.RetentionPolicySummary}";
            }
            catch (Exception ex)
            {
                DataHandlingDisclosureText.Text = $"Disclosure unavailable: {ex.Message}";
            }
        }

        private void RefreshDataHandlingDisclosure_Click(object sender, RoutedEventArgs e)
        {
            RefreshDataHandlingDisclosure();
        }

        private void ExportComplianceAudit_Click(object sender, RoutedEventArgs e)
        {
            EntitlementCheckResult auditGate = _planEntitlementService.CheckFeature(EntitlementFeature.AuditExport);
            if (!auditGate.Allowed)
            {
                ComplianceAuditStatusText.Text = auditGate.Message;
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv|JSONL file (*.jsonl)|*.jsonl",
                Title = "Export compliance audit log",
                FileName = "compliance-audit-log.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                if (dialog.FileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
                {
                    _complianceAuditService.ExportJsonl(dialog.FileName);
                }
                else
                {
                    _complianceAuditService.ExportCsv(dialog.FileName);
                }

                ComplianceAuditStatusText.Text = $"Compliance audit exported: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                ComplianceAuditStatusText.Text = $"Compliance audit export failed: {ex.Message}";
            }
        }

        private void TelemetrySetting_Changed(object sender, RoutedEventArgs e)
        {
            if (sender == ConfidentialProjectModeBox && ConfidentialityModeCombo != null)
            {
                if (ConfidentialProjectModeBox.IsChecked == true)
                {
                    ConfidentialityModeCombo.SelectedItem = FindConfidentialityModeItem("LocalOnly");
                }
                else if (ConfidentialityModeCombo.SelectedItem is ComboBoxItem selected
                         && string.Equals(selected.Content?.ToString(), "LocalOnly", StringComparison.OrdinalIgnoreCase))
                {
                    ConfidentialityModeCombo.SelectedItem = FindConfidentialityModeItem("Standard");
                }
            }

            RefreshTelemetryLockState();
            RefreshDataHandlingDisclosure();
        }

        private void TelemetryText_Changed(object sender, TextChangedEventArgs e)
        {
            RefreshTelemetryLockState();
            RefreshDataHandlingDisclosure();
        }

        private void AccountIdentity_Changed(object sender, TextChangedEventArgs e)
        {
            RefreshTelemetryLockState();
            RefreshDataHandlingDisclosure();
        }

        private void HotkeyText_Changed(object sender, TextChangedEventArgs e)
        {
            RefreshHotkeyConflictState();
        }

        private bool ValidateHotkeys()
        {
            bool showValid = HotkeyBindingService.TryParse(ShowPanelHotkeyBox.Text, "Show overlay", out HotkeyBinding showBinding);
            bool inPlaceValid = HotkeyBindingService.TryParse(TranslateInPlaceHotkeyBox.Text, "In-place translation", out HotkeyBinding inPlaceBinding);
            if (!showValid || !inPlaceValid)
            {
                HotkeyConflictStatusText.Text = "Invalid hotkey format. Example: Ctrl+Space or Ctrl+Shift+Space.";
                HotkeyConflictStatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
                return false;
            }

            IReadOnlyList<string> conflicts = HotkeyBindingService.FindConflicts(showBinding, inPlaceBinding);
            if (conflicts.Count > 0)
            {
                HotkeyConflictStatusText.Text = "Hotkey conflict detected. Use unique combinations.";
                HotkeyConflictStatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
                return false;
            }

            return true;
        }

        private void RefreshHotkeyConflictState()
        {
            bool showValid = HotkeyBindingService.TryParse(ShowPanelHotkeyBox.Text, "Show overlay", out HotkeyBinding showBinding);
            bool inPlaceValid = HotkeyBindingService.TryParse(TranslateInPlaceHotkeyBox.Text, "In-place translation", out HotkeyBinding inPlaceBinding);
            if (!showValid || !inPlaceValid)
            {
                HotkeyConflictStatusText.Text = "Invalid hotkey format. Example: Ctrl+Space or Ctrl+Shift+Space.";
                HotkeyConflictStatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
                return;
            }

            IReadOnlyList<string> conflicts = HotkeyBindingService.FindConflicts(showBinding, inPlaceBinding);
            if (conflicts.Count > 0)
            {
                HotkeyConflictStatusText.Text = $"Hotkey conflict: {string.Join("; ", conflicts)}.";
                HotkeyConflictStatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
                return;
            }

            HotkeyConflictStatusText.Text = "Hotkey configuration is valid.";
            HotkeyConflictStatusText.Foreground = System.Windows.Media.Brushes.LightBlue;
        }

        private void ConfidentialityModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfidentialityModeCombo.SelectedItem is ComboBoxItem item && item.Content is string mode)
            {
                if (!_planEntitlementService.IsConfidentialityModeAllowed(mode))
                {
                    ConfidentialityModeCombo.SelectedItem = FindConfidentialityModeItem("Standard");
                    DataHandlingDisclosureText.Text = _planEntitlementService
                        .CheckFeature(EntitlementFeature.ConfidentialityModes)
                        .Message;
                    return;
                }

                if (string.Equals(mode, "LocalOnly", StringComparison.OrdinalIgnoreCase))
                {
                    ConfidentialProjectModeBox.IsChecked = true;
                }
                else if (ConfidentialProjectModeBox.IsChecked == true)
                {
                    ConfidentialProjectModeBox.IsChecked = false;
                }
            }

            RefreshDataHandlingDisclosure();
        }

        private void RefreshTelemetryLockState()
        {
            string accountId = string.IsNullOrWhiteSpace(AccountIdBox.Text) ? "account-local" : AccountIdBox.Text.Trim();
            bool lockEnabled = TelemetryConsentLockBox.IsChecked == true;
            string lockedBy = SettingsService.Current.TelemetryConsentLockedByAccountId;

            bool lockedByDifferentAccount = lockEnabled
                && !string.IsNullOrWhiteSpace(lockedBy)
                && !string.Equals(lockedBy, accountId, StringComparison.OrdinalIgnoreCase);

            TelemetryUsageMetricsBox.IsEnabled = !lockedByDifferentAccount;
            TelemetryCrashDiagnosticsBox.IsEnabled = !lockedByDifferentAccount;
            TelemetryModelOutputBox.IsEnabled = !lockedByDifferentAccount;

            TelemetryLockStatusText.Text = lockedByDifferentAccount
                ? $"Telemetry consent is locked by account '{lockedBy}'."
                : (lockEnabled
                    ? $"Telemetry consent lock enabled for account '{(string.IsNullOrWhiteSpace(lockedBy) ? accountId : lockedBy)}'."
                    : "Telemetry consent lock is not enabled.");
        }

        private ComboBoxItem? FindConfidentialityModeItem(string mode)
        {
            foreach (var item in ConfidentialityModeCombo.Items)
            {
                if (item is ComboBoxItem combo && string.Equals(combo.Content?.ToString(), mode, StringComparison.OrdinalIgnoreCase))
                {
                    return combo;
                }
            }

            return ConfidentialityModeCombo.Items.Count > 0 ? ConfidentialityModeCombo.Items[0] as ComboBoxItem : null;
        }

        private PmfDashboardSnapshot GetCurrentWeekSnapshot()
        {
            DateTime end = DateTime.UtcNow.Date;
            DateTime start = end.AddDays(-7);
            return _pmfDashboardService.GetDashboardSnapshot(start, end);
        }

        private void CreateProjectFromTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (NicheTemplateCombo.SelectedItem is not NicheProjectTemplate selectedTemplate)
            {
                NicheTemplateStatusText.Text = "Select a niche template first.";
                return;
            }

            try
            {
                string projectName = string.IsNullOrWhiteSpace(NicheProjectNameBox.Text)
                    ? selectedTemplate.Name
                    : NicheProjectNameBox.Text.Trim();

                ProjectNicheConfiguration config = _nicheTemplateService.CreateProjectFromTemplate(
                    selectedTemplate.TemplateId,
                    projectName,
                    SettingsService.Current.TargetLanguage);

                NicheProjectNameBox.Text = config.ProjectProfileName;
                NicheTemplateStatusText.Text =
                    $"Created project '{config.ProjectProfileName}' from {selectedTemplate.Name}. " +
                    $"Domain: {config.Domain}, Terms seeded: {selectedTemplate.StarterGlossaryTerms.Count}.";
            }
            catch (Exception ex)
            {
                NicheTemplateStatusText.Text = $"Template creation failed: {ex.Message}";
            }
        }

        private void ExportNichePack_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Niche pack (*.segmentniche.json)|*.segmentniche.json|JSON file (*.json)|*.json",
                Title = "Export niche pack",
                FileName = $"{GlossaryService.CurrentProfile.Name}-niche-pack.segmentniche.json"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                string userId = string.IsNullOrWhiteSpace(ViralUserIdBox.Text) ? "user-local" : ViralUserIdBox.Text.Trim();
                string projectName = string.IsNullOrWhiteSpace(NicheProjectNameBox.Text)
                    ? GlossaryService.CurrentProfile.Name
                    : NicheProjectNameBox.Text.Trim();

                _nicheTemplateService.ExportPack(
                    dialog.FileName,
                    projectName,
                    userId,
                    packName: $"{projectName} Niche Pack");

                NicheTemplateStatusText.Text = $"Niche pack exported: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                NicheTemplateStatusText.Text = $"Niche export failed: {ex.Message}";
            }
        }

        private void ImportNichePack_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Niche pack (*.segmentniche.json)|*.segmentniche.json|JSON file (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import niche pack"
            };

            if (dialog.ShowDialog() != true) return;

            var policyChoice = MessageBox.Show(
                "Duplicate terms found during import: select Yes to overwrite existing terms, No to keep existing terms.",
                "Niche Pack Conflict Policy",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (policyChoice == MessageBoxResult.Cancel)
            {
                return;
            }

            NichePackConflictMode conflictMode = policyChoice == MessageBoxResult.Yes
                ? NichePackConflictMode.OverwriteExisting
                : NichePackConflictMode.KeepExisting;

            try
            {
                string projectName = string.IsNullOrWhiteSpace(NicheProjectNameBox.Text)
                    ? "Imported Niche Project"
                    : NicheProjectNameBox.Text.Trim();

                NichePackImportResult result = _nicheTemplateService.ImportPack(
                    dialog.FileName,
                    projectName,
                    SettingsService.Current.TargetLanguage,
                    conflictMode);

                NicheProjectNameBox.Text = result.TargetProfileName;
                NicheTemplateStatusText.Text =
                    $"Imported pack '{result.Metadata.PackName}' into '{result.TargetProfileName}'. " +
                    $"Inserted: {result.InsertedTermCount}, Updated: {result.UpdatedTermCount}, Skipped: {result.SkippedTermCount}, Conflicts: {result.DuplicateConflictCount}.";
            }
            catch (Exception ex)
            {
                NicheTemplateStatusText.Text = $"Niche import failed: {ex.Message}";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            (_gtmConfigService as IDisposable)?.Dispose();
            (_pilotWorkspaceService as IDisposable)?.Dispose();
            (_accountMetadataService as IDisposable)?.Dispose();
            (_pmfDashboardService as IDisposable)?.Dispose();
            (_nicheTelemetryService as IDisposable)?.Dispose();
            _referralService.Dispose();
            base.OnClosed(e);
        }

        private void ExportLegalPack_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Glossary pack (*.json)|*.json",
                Title = "Export legal glossary pack",
                FileName = "legal-glossary-pack.json"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                string userId = string.IsNullOrWhiteSpace(ViralUserIdBox.Text) ? "user-local" : ViralUserIdBox.Text.Trim();
                string referralCode = string.IsNullOrWhiteSpace(ViralReferralCodeBox.Text)
                    ? _referralService.CreateReferralCode(userId)
                    : ViralReferralCodeBox.Text.Trim();

                var metadata = _glossaryPackSharingService.ExportLegalGlossaryPack(
                    dialog.FileName,
                    userId,
                    "Shared Legal Pack",
                    referralCode,
                    isGlobal: true);

                ViralReferralCodeBox.Text = metadata.ReferralCode;
                MessageBox.Show($"Pack exported. Referral code: {metadata.ReferralCode}", "Viral Share", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}");
            }
        }

        private void ImportSharedPack_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Glossary pack (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import shared glossary pack"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                string userId = string.IsNullOrWhiteSpace(ViralUserIdBox.Text) ? "user-local" : ViralUserIdBox.Text.Trim();
                var result = _glossaryPackSharingService.ImportGlossaryPack(dialog.FileName, userId, isGlobal: false);

                if (!string.IsNullOrWhiteSpace(result.Metadata.ReferralCode))
                {
                    _referralService.RecordGlossaryImportedMilestone(userId);
                }

                MessageBox.Show($"Imported {result.InsertedTermCount} terms. Attribution code: {result.Metadata.ReferralCode}", "Viral Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}");
            }
        }
    }
}
