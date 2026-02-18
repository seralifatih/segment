using System;
using System.Windows;
using System.Windows.Input;
using Segment.App.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Clipboard = System.Windows.Clipboard;
using Segment.App.Models;
using System.Windows.Threading;

namespace Segment.App.Views
{
    public partial class FloatingPanel : Window
    {
        private const double ShortSegmentP95ThresholdMs = 700;
        private string _sourceText = "";
        private string _aiRawOutput = "";
        private readonly IGtmConfigService _gtmConfigService;
        private readonly ITranslationGuardrailEngine _guardrailEngine;
        private readonly TranslationPastebackCoordinator _pastebackCoordinator;
        private readonly INicheTemplateService _nicheTemplateService;
        private readonly OverlayWorkflowController _workflowController;
        private readonly INicheTelemetryService _telemetryService;
        private readonly IPlanEntitlementService _planEntitlementService;
        private readonly StructuredLogger _logger;
        private IReadOnlyList<GuardrailResult> _activeBlockingIssues = Array.Empty<GuardrailResult>();
        private bool _hasUndoSafePaste;
        private string _clipboardBeforeSafePaste = "";
        private string _expectedClipboardSnapshotForPaste = "";
        private string _sourceSegmentHash = "";
        private int _lastGlossaryHitCount;
        private bool _pendingApplyInPlace;
        private IReadOnlyList<GuardrailResult> _pendingWarningIssues = Array.Empty<GuardrailResult>();
        private string _pendingWarningOutput = "";
        private bool _pendingWarningPasteInHostApp;
        private string _pendingWarningExpectedClipboardSnapshot = "";

        public FloatingPanel()
        {
            InitializeComponent();
            _gtmConfigService = new GtmConfigService();
            _guardrailEngine = new TranslationGuardrailEngine();
            _pastebackCoordinator = new TranslationPastebackCoordinator(_guardrailEngine);
            _nicheTemplateService = new NicheTemplateService();
            _workflowController = new OverlayWorkflowController();
            _telemetryService = new NicheTelemetryService();
            _planEntitlementService = new PlanEntitlementService();
            _logger = new StructuredLogger();

            this.PreviewKeyDown += Window_PreviewKeyDown;
            this.MouseLeftButtonDown += (s, e) => { try { this.DragMove(); } catch { } };

            LoadProfiles();
            RefreshLaunchPhaseBadge();
            RefreshConfidentialityRouteIndicator();
            RefreshDomainAndScopeBadges();
            SetWorkflowState(OverlayWorkflowState.Captured);
            RefreshUndoButtonState();
            RefreshGuardrailOverrideAvailability();
            this.SourceInitialized += (s, e) => LearningManager.Initialize(this);
        }

        private void LoadProfiles()
        {
            ProfileCombo.ItemsSource = GlossaryService.Profiles.Select(p => p.Name).ToList();
            ProfileCombo.SelectedItem = GlossaryService.CurrentProfile.Name;
            RefreshDomainAndScopeBadges();

            // YENİ: Profil yüklendiğinde Freeze durumunu güncelle
            UpdateFreezeState();
        }

        private void ProfileCombo_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string text = ProfileCombo.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    GlossaryService.GetOrCreateProfile(text);
                    LoadProfiles();
                    ProfileCombo.SelectedItem = text;
                    InputText.Focus();
                    RefreshDomainAndScopeBadges();

                    // YENİ: Yeni profil oluşturulunca durumunu kontrol et
                    UpdateFreezeState();
                }
            }
        }

        private void ProfileCombo_DropDownClosed(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ProfileCombo.Text))
            {
                GlossaryService.GetOrCreateProfile(ProfileCombo.Text);
                RefreshDomainAndScopeBadges();
                // YENİ: Seçim değişince buton durumunu güncelle
                UpdateFreezeState();
            }
        }

        // --- YENİ: FREEZE MODE MANTIĞI 🧊 ---
        private void UpdateFreezeState()
        {
            if (GlossaryService.CurrentProfile != null)
            {
                // Butonu profilin kayıtlı durumuna göre ayarla
                FreezeButton.IsChecked = GlossaryService.CurrentProfile.IsFrozen;
            }
        }

        private void FreezeButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = GlossaryService.CurrentProfile;
            if (profile == null) return;

            // Durumu güncelle (Mavi basılıysa dondur, değilse çöz)
            profile.IsFrozen = FreezeButton.IsChecked == true;

            // Kaydet
            GlossaryService.SaveProfile(profile);
        }
        // -------------------------------------

        public async void HandleSmartHotkey()
        {
            try
            {
                if (this.IsVisible)
                {
                    CopyResultAndClose();
                    return;
                }

                DateTime capturedAtUtc = DateTime.UtcNow;
                string clipboardText = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
                _sourceText = clipboardText;
                _expectedClipboardSnapshotForPaste = clipboardText;
                _pendingApplyInPlace = false;
                _sourceSegmentHash = _telemetryService.HashSegment(_sourceText);

                this.Show();
                this.Activate();
                TermPanel.Visibility = Visibility.Collapsed;
                TermList.ItemsSource = null;
                GuardrailIssuePanel.Visibility = Visibility.Collapsed;
                GuardrailIssueList.ItemsSource = null;
                _activeBlockingIssues = Array.Empty<GuardrailResult>();
                _pendingWarningIssues = Array.Empty<GuardrailResult>();
                _lastGlossaryHitCount = 0;
                SetWorkflowState(OverlayWorkflowState.Captured);
                RefreshUndoButtonState();

                // Her açılışta buton durumunu tazele (Garanti olsun)
                UpdateFreezeState();
                RefreshDomainAndScopeBadges();

                InputText.Text = clipboardText;
                InputText.Focus();
                InputText.SelectAll();

                if (!string.IsNullOrEmpty(clipboardText))
                {
                    await PerformTranslationAsync(clipboardText, capturedAtUtc);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("overlay_hotkey_exception", ex);
                ActivateCrashSafeFallback("Could not open reflex workflow. Fallback mode is active.");
            }
        }

        public async void HandleTranslateSelectionInPlaceFallback()
        {
            try
            {
                string previousClipboard = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
                System.Windows.Forms.SendKeys.SendWait("^c");
                await Task.Delay(120);
                string selectedText = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(selectedText))
                {
                    HandleSmartHotkey();
                    return;
                }

                _sourceText = selectedText;
                _expectedClipboardSnapshotForPaste = previousClipboard;
                _pendingApplyInPlace = true;
                _sourceSegmentHash = _telemetryService.HashSegment(_sourceText);
                SetWorkflowState(OverlayWorkflowState.Translating);
                RecordTelemetryEvent(
                    NicheTelemetryEventType.TranslationRequested,
                    success: true);
                DateTime capturedAtUtc = DateTime.UtcNow;
                DateTime requestStartedUtc = DateTime.UtcNow;
                TranslationExecutionResult execution = await TranslationService.SuggestWithMetricsAsync(selectedText);
                DateTime responseReceivedUtc = DateTime.UtcNow;
                string translated = execution.OutputText;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                DateTime renderCompletedUtc = DateTime.UtcNow;
                RecordLatencySample(capturedAtUtc, requestStartedUtc, responseReceivedUtc, renderCompletedUtc, selectedText, execution);

                double endToEndMs = (renderCompletedUtc - capturedAtUtc).TotalMilliseconds;
                if (string.IsNullOrWhiteSpace(translated) || translated.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    RecordTelemetryEvent(
                        NicheTelemetryEventType.TranslationCompleted,
                        success: false,
                        latencyMs: endToEndMs);
                    this.Show();
                    this.Activate();
                    OutputText.Text = translated;
                    HandleTranslationFailure(translated);
                    return;
                }
                RecordTelemetryEvent(
                    NicheTelemetryEventType.TranslationCompleted,
                    success: true,
                    latencyMs: endToEndMs);
                RecordTelemetryEvent(
                    NicheTelemetryEventType.SuggestionAccepted,
                    success: true);

                _aiRawOutput = translated;
                OutputText.Text = translated;
                await ApplyGuardrailedResultAsync(translated, pasteInHostApp: true, expectedClipboardSnapshot: previousClipboard);
            }
            catch (Exception ex)
            {
                _logger.Error("overlay_inplace_exception", ex);
                HandleSmartHotkey();
            }
        }

        private async Task PerformTranslationAsync(string text, DateTime? capturedAtUtc = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                _sourceText = text;
                _sourceSegmentHash = _telemetryService.HashSegment(_sourceText);
                _lastGlossaryHitCount = 0;
                SetWorkflowState(OverlayWorkflowState.Translating);
                RecordTelemetryEvent(
                    NicheTelemetryEventType.TranslationRequested,
                    success: true);
                OutputText.Text = "AI is translating...";
                OutputText.Opacity = 0.6;

                DateTime effectiveCaptureUtc = capturedAtUtc ?? DateTime.UtcNow;
                DateTime requestStartedUtc = DateTime.UtcNow;
                TranslationExecutionResult execution = await TranslationService.SuggestWithMetricsAsync(text);
                DateTime responseReceivedUtc = DateTime.UtcNow;
                string result = execution.OutputText;

                _aiRawOutput = result;
                OutputText.Text = result;
                RefreshConfidentialityRouteIndicator();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                DateTime renderCompletedUtc = DateTime.UtcNow;
                RecordLatencySample(effectiveCaptureUtc, requestStartedUtc, responseReceivedUtc, renderCompletedUtc, text, execution);
                double endToEndMs = (renderCompletedUtc - effectiveCaptureUtc).TotalMilliseconds;
                if (result.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    HandleTranslationFailure(result);
                    RecordTelemetryEvent(
                        NicheTelemetryEventType.TranslationCompleted,
                        success: false,
                        latencyMs: endToEndMs);
                }
                else
                {
                    SetWorkflowState(OverlayWorkflowState.Ready);
                    RecordTelemetryEvent(
                        NicheTelemetryEventType.TranslationCompleted,
                        success: true,
                        latencyMs: endToEndMs);
                }

                CheckAndDisplayTerms(text);
            }
            catch (Exception ex)
            {
                OutputText.Text = $"ERROR: {ex.Message}";
                SetWorkflowState(OverlayWorkflowState.Error, ex.Message);
                RecordTelemetryEvent(
                    NicheTelemetryEventType.TranslationCompleted,
                    success: false);
                _logger.Error("overlay_translate_exception", ex);
                ActivateCrashSafeFallback("Translation failed unexpectedly. Original text is preserved.");
            }
            finally { OutputText.Opacity = 1.0; }
        }

        private void ActivateCrashSafeFallback(string message)
        {
            SetWorkflowState(OverlayWorkflowState.Error, message);
            try
            {
                if (!string.IsNullOrWhiteSpace(_sourceText))
                {
                    Clipboard.SetText(_sourceText);
                }
            }
            catch
            {
                // Best effort fallback.
            }

            OutputText.Text =
                $"{message}\n" +
                "Original source text has been restored to clipboard for manual paste.";
            GuardrailIssuePanel.Visibility = Visibility.Collapsed;
            TermPanel.Visibility = Visibility.Collapsed;
        }

        private void CheckAndDisplayTerms(string sourceText)
        {
            // EffectiveTerms kullanarak hem Global hem Proje kurallarını gösteriyoruz
            var allTerms = GlossaryService.GetEffectiveTerms();
            if (allTerms.Count == 0) return;

            var detectedTerms = new List<TermDisplay>();

            foreach (var kvp in allTerms)
            {
                // Basit contains kontrolü (Performans için)
                if (sourceText.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    detectedTerms.Add(new TermDisplay
                    {
                        Source = kvp.Key,
                        Target = kvp.Value.Target
                    });
                }
            }

            if (detectedTerms.Count > 0)
            {
                TermList.ItemsSource = detectedTerms;
                TermPanel.Visibility = Visibility.Visible;
                _lastGlossaryHitCount = detectedTerms.Count;
                RecordTelemetryEvent(
                    NicheTelemetryEventType.GlossaryTermApplied,
                    success: true,
                    glossaryHitCount: detectedTerms.Count);
            }
            else
            {
                _lastGlossaryHitCount = 0;
            }
        }

        private async void CopyResultAndClose()
        {
            string finalUserOutput = OutputText.Text;

            if (!string.IsNullOrWhiteSpace(finalUserOutput) && !finalUserOutput.StartsWith("ERROR"))
            {
                bool suggestionEdited = !string.Equals(
                    (_aiRawOutput ?? string.Empty).Trim(),
                    finalUserOutput.Trim(),
                    StringComparison.Ordinal);
                RecordTelemetryEvent(
                    suggestionEdited ? NicheTelemetryEventType.SuggestionEdited : NicheTelemetryEventType.SuggestionAccepted,
                    success: true,
                    glossaryHitCount: _lastGlossaryHitCount);
                await ApplyGuardrailedResultAsync(finalUserOutput, pasteInHostApp: false, expectedClipboardSnapshot: _expectedClipboardSnapshotForPaste);
            }
        }

        private bool ExecuteSafePasteAndClose(string finalUserOutput, bool pasteInHostApp, string expectedClipboardSnapshot)
        {
            string currentClipboard = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            ClipboardCollisionDecision collision = ClipboardSafetyService.EvaluateOverwrite(expectedClipboardSnapshot, currentClipboard);
            if (!collision.AllowOverwrite)
            {
                SetWorkflowState(OverlayWorkflowState.Error, collision.Reason);
                OutputText.Text = $"Paste cancelled: {collision.Reason}";
                return false;
            }

            try
            {
                _clipboardBeforeSafePaste = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch
            {
                _clipboardBeforeSafePaste = string.Empty;
            }

            try
            {
                Clipboard.SetText(finalUserOutput);
                _hasUndoSafePaste = true;
            }
            catch
            {
                _hasUndoSafePaste = false;
            }

            if (pasteInHostApp)
            {
                try
                {
                    System.Windows.Forms.SendKeys.SendWait("^v");
                }
                catch (Exception ex)
                {
                    SetWorkflowState(OverlayWorkflowState.Error, ex.Message);
                    OutputText.Text = $"ERROR: In-place paste failed ({ex.Message})";
                    return false;
                }
            }

            SetWorkflowState(OverlayWorkflowState.Applied);
            RefreshUndoButtonState();
            LearningManager.StartMonitoring(_sourceText, finalUserOutput);
            RecordTelemetryEvent(
                NicheTelemetryEventType.PasteCompleted,
                success: true,
                glossaryHitCount: _lastGlossaryHitCount);
            this.Hide();
            return true;
        }

        private TranslationContext BuildTranslationContext()
        {
            DomainVertical domain = Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical parsed)
                ? parsed
                : DomainVertical.Legal;

            var terminology = GlossaryService.GetEffectiveTerms()
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value?.Target))
                .ToDictionary(x => x.Key, x => x.Value.Target, StringComparer.OrdinalIgnoreCase);

            var context = new TranslationContext
            {
                Domain = domain,
                SourceLanguage = "English",
                TargetLanguage = SettingsService.Current.TargetLanguage,
                LockedTerminology = terminology,
                AccountId = SettingsService.Current.AccountId,
                StrictQaMode = SettingsService.Current.QaStrictMode
            };

            if (_nicheTemplateService.TryGetProjectConfiguration(GlossaryService.CurrentProfile.Name, out ProjectNicheConfiguration config))
            {
                context.Domain = config.Domain;
                context.ActiveStyleHints = config.StyleHints;
                context.EnabledQaChecks = config.EnabledQaChecks;
            }

            EntitlementCheckResult guardrailGate = _planEntitlementService.CheckFeature(EntitlementFeature.AdvancedGuardrails);
            if (!guardrailGate.Allowed)
            {
                context.EnabledQaChecks = new[] { LegalDomainQaPlugin.Id };
            }

            return context;
        }

        private void ShowBlockingGuardrailMessage(IReadOnlyList<GuardrailResult> issues)
        {
            string summary = string.Join("\n", issues
                .Take(4)
                .Select(x => $"- {x.RuleId}: {x.Message}"));

            OutputText.Text =
                "Domain QA guardrails blocked paste-back due to validation checks.\n" +
                summary +
                "\nUse override only with explicit reviewer justification.";
            GuardrailIssueList.ItemsSource = issues.ToList();
            GuardrailIssuePanel.Visibility = Visibility.Visible;
            GuardrailActionHintText.Visibility = Visibility.Collapsed;
            ApplyWarningsAnywayButton.Visibility = Visibility.Collapsed;
            OverrideBlockedIssuesButton.Visibility = SettingsService.Current.AllowGuardrailOverrides
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ShowWarningGuardrailMessage(IReadOnlyList<GuardrailResult> issues)
        {
            string summary = string.Join("\n", issues
                .Take(4)
                .Select(x => $"- {x.RuleId}: {x.Message}"));

            OutputText.Text =
                "Pre-apply QA warning summary:\n" +
                summary +
                "\nPress [R] to review details or [A] to apply anyway.";
            GuardrailIssueList.ItemsSource = issues.ToList();
            GuardrailIssuePanel.Visibility = Visibility.Visible;
            GuardrailActionHintText.Visibility = Visibility.Visible;
            ApplyWarningsAnywayButton.Visibility = Visibility.Visible;
            OverrideBlockedIssuesButton.Visibility = Visibility.Collapsed;
        }

        private async Task ApplyGuardrailedResultAsync(string finalUserOutput, bool pasteInHostApp, string expectedClipboardSnapshot)
        {
            var context = BuildTranslationContext();
            PastebackDecision decision = _pastebackCoordinator.Evaluate(_sourceText, finalUserOutput, context);
            if (!decision.AutoPasteAllowed)
            {
                this.Show();
                this.Activate();
                _activeBlockingIssues = decision.BlockingIssues;
                ShowBlockingGuardrailMessage(decision.BlockingIssues);
                SetWorkflowState(OverlayWorkflowState.Error, "Guardrail validation blocked paste.");
                RecordTelemetryEvent(
                    NicheTelemetryEventType.GuardrailBlocked,
                    success: false,
                    blockedCount: decision.BlockingIssues.Count,
                    glossaryHitCount: _lastGlossaryHitCount);
                return;
            }

            IReadOnlyList<GuardrailResult> warningIssues = decision.Validation.Results
                .Where(x => !x.IsBlocking && x.Severity >= GuardrailSeverity.Warning)
                .ToList();
            if (warningIssues.Count > 0)
            {
                this.Show();
                this.Activate();
                _pendingWarningIssues = warningIssues;
                _pendingWarningOutput = finalUserOutput;
                _pendingWarningPasteInHostApp = pasteInHostApp;
                _pendingWarningExpectedClipboardSnapshot = expectedClipboardSnapshot;
                ShowWarningGuardrailMessage(warningIssues);
                SetWorkflowState(OverlayWorkflowState.Ready);
                return;
            }

            bool applied = ExecuteSafePasteAndClose(finalUserOutput, pasteInHostApp, expectedClipboardSnapshot);
            if (!applied)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        private void HandleTranslationFailure(string result)
        {
            string errorText = string.IsNullOrWhiteSpace(result) ? "Translation failed." : result;
            SetWorkflowState(OverlayWorkflowState.Error, errorText);
            if (errorText.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show(
                    "Translation timed out before completion. You can retry or switch provider.",
                    "Translation Timeout",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void SetWorkflowState(OverlayWorkflowState state, string? error = null)
        {
            switch (state)
            {
                case OverlayWorkflowState.Captured:
                    _workflowController.MarkCaptured();
                    break;
                case OverlayWorkflowState.Translating:
                    _workflowController.MarkTranslating();
                    break;
                case OverlayWorkflowState.Ready:
                    _workflowController.MarkReady();
                    break;
                case OverlayWorkflowState.Applied:
                    _workflowController.MarkApplied();
                    break;
                case OverlayWorkflowState.Error:
                    _workflowController.MarkError(error);
                    break;
            }

            WorkflowStateText.Text = $"State: {_workflowController.BuildLabel()}";
            WorkflowStateText.Foreground = state == OverlayWorkflowState.Error
                ? System.Windows.Media.Brushes.LightCoral
                : System.Windows.Media.Brushes.White;
        }

        private void RefreshUndoButtonState()
        {
            UndoSafePasteButton.Visibility = _hasUndoSafePaste ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshDomainAndScopeBadges()
        {
            DomainVertical domain = Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical parsed)
                ? parsed
                : DomainVertical.Legal;

            if (_nicheTemplateService.TryGetProjectConfiguration(GlossaryService.CurrentProfile.Name, out ProjectNicheConfiguration config))
            {
                domain = config.Domain;
                GlossaryScopeBadgeText.Text = $"Scope: Project+Global ({config.ProjectProfileName})";
            }
            else
            {
                GlossaryScopeBadgeText.Text = $"Scope: Project+Global ({GlossaryService.CurrentProfile.Name})";
            }

            ActiveDomainBadgeText.Text = $"Domain: {domain}";
        }

        private DomainVertical ResolveActiveDomain()
        {
            if (_nicheTemplateService.TryGetProjectConfiguration(GlossaryService.CurrentProfile.Name, out ProjectNicheConfiguration config))
            {
                return config.Domain;
            }

            return Enum.TryParse(SettingsService.Current.ActiveDomain, out DomainVertical parsed)
                ? parsed
                : DomainVertical.Legal;
        }

        private void RecordTelemetryEvent(
            NicheTelemetryEventType eventType,
            bool success,
            double latencyMs = 0,
            int blockedCount = 0,
            int overrideCount = 0,
            int glossaryHitCount = 0)
        {
            try
            {
                string hash = string.IsNullOrWhiteSpace(_sourceSegmentHash)
                    ? _telemetryService.HashSegment(_sourceText)
                    : _sourceSegmentHash;
                var telemetryEvent = _telemetryService.BuildEvent(
                    eventType,
                    ResolveActiveDomain(),
                    hash,
                    success,
                    latencyMs,
                    blockedCount,
                    overrideCount,
                    glossaryHitCount);
                _telemetryService.RecordEvent(telemetryEvent);
            }
            catch
            {
                // Telemetry must not disrupt translation UX flow.
            }
        }

        private void UndoSafePaste_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasUndoSafePaste)
            {
                return;
            }

            try
            {
                Clipboard.SetText(_clipboardBeforeSafePaste ?? string.Empty);
                OutputText.Text = "Undo-safe paste restored previous clipboard content.";
                _hasUndoSafePaste = false;
                RefreshUndoButtonState();
                SetWorkflowState(OverlayWorkflowState.Ready);
                RecordTelemetryEvent(
                    NicheTelemetryEventType.PasteReverted,
                    success: true);
            }
            catch (Exception ex)
            {
                OutputText.Text = $"Undo failed: {ex.Message}";
            }
        }

        private void ApplyQuickFix_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not GuardrailResult issue)
            {
                return;
            }

            if (issue.RuleId == "LEGAL_NUMERIC_MISMATCH" || issue.RuleId == "LEGAL_DATE_MISMATCH" || issue.RuleId == "FIN_NUMERIC_FIDELITY")
            {
                OutputText.Text += "\n" + $"[QuickFix] Verify numeric/date tokens against source: {_sourceText}";
            }
            else if (issue.RuleId == "LEGAL_LOCKED_TERMINOLOGY")
            {
                OutputText.Text += "\n" + $"[QuickFix] {issue.SuggestedFix}";
            }
            else
            {
                OutputText.Text += "\n" + $"[QuickFix] {issue.SuggestedFix}";
            }
        }

        private void OverrideBlockedIssues_Click(object sender, RoutedEventArgs e)
        {
            if (!SettingsService.Current.AllowGuardrailOverrides)
            {
                System.Windows.MessageBox.Show(
                    "Guardrail overrides are disabled by policy.",
                    "Override Blocked",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            if (_activeBlockingIssues == null || _activeBlockingIssues.Count == 0)
            {
                return;
            }

            if (!TryBypassBlockingIssues(_activeBlockingIssues, out string overrideReason))
            {
                return;
            }

            var context = BuildTranslationContext();
            ComplianceAuditService.Default.Record(new ComplianceAuditRecord
            {
                EventType = ComplianceAuditEventType.GuardrailOverride,
                AccountId = SettingsService.Current.AccountId,
                Decision = "translation_pasteback_override",
                ActiveMode = SettingsService.Current.ConfidentialProjectLocalOnly ? "Confidential Local-Only" : "Standard",
                ProviderRoute = SettingsService.Current.AiProvider,
                RetentionPolicySummary = SettingsService.Current.RetentionPolicySummary,
                Details = $"Domain guardrail override applied. Reason: {overrideReason}",
                Metadata = new Dictionary<string, string>
                {
                    ["domain"] = context.Domain.ToString(),
                    ["blocking_issue_count"] = _activeBlockingIssues.Count.ToString()
                }
            });
            RecordTelemetryEvent(
                NicheTelemetryEventType.GuardrailOverridden,
                success: true,
                blockedCount: _activeBlockingIssues.Count,
                overrideCount: 1,
                glossaryHitCount: _lastGlossaryHitCount);

            GuardrailIssuePanel.Visibility = Visibility.Collapsed;
            ExecuteSafePasteAndClose(OutputText.Text, pasteInHostApp: _pendingApplyInPlace, expectedClipboardSnapshot: _expectedClipboardSnapshotForPaste);
        }

        private static bool TryBypassBlockingIssues(IReadOnlyList<GuardrailResult> blockingIssues, out string reason)
        {
            reason = string.Empty;
            string summary = string.Join("\n", blockingIssues.Select(x => $"{x.RuleId}: {x.Message}"));

            var confirm = System.Windows.MessageBox.Show(
                "Blocking guardrail issues were detected:\n\n" +
                $"{summary}\n\n" +
                "Do you want to override and continue paste-back?",
                "Domain Guardrail Validation",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes)
            {
                return false;
            }

            if (!TryPromptOverrideReason(out reason))
            {
                System.Windows.MessageBox.Show(
                    "Override reason is required to bypass blocking guardrails.",
                    "Override Denied",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return false;
            }

            return true;
        }

        private static bool TryPromptOverrideReason(out string reason)
        {
            reason = string.Empty;

            var dialog = new Window
            {
                Title = "Guardrail Override Reason",
                Width = 480,
                Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow
            };

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var text = new System.Windows.Controls.TextBlock
            {
                Text = "Provide justification for bypassing blocking guardrail results:",
                TextWrapping = System.Windows.TextWrapping.Wrap
            };

            var reasonBox = new System.Windows.Controls.TextBox
            {
                Margin = new Thickness(0, 12, 0, 12),
                AcceptsReturn = true,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
            };

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
            var okButton = new System.Windows.Controls.Button
            {
                Content = "Confirm Override",
                Width = 130,
                Background = System.Windows.Media.Brushes.DarkRed,
                Foreground = System.Windows.Media.Brushes.White
            };

            cancelButton.Click += (_, _) => { dialog.DialogResult = false; dialog.Close(); };
            okButton.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(reasonBox.Text))
                {
                    System.Windows.MessageBox.Show(
                        "Reason is required.",
                        "Validation",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                dialog.DialogResult = true;
                dialog.Close();
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);

            System.Windows.Controls.Grid.SetRow(text, 0);
            System.Windows.Controls.Grid.SetRow(reasonBox, 1);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(text);
            grid.Children.Add(reasonBox);
            grid.Children.Add(buttonPanel);
            dialog.Content = grid;

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                reason = reasonBox.Text.Trim();
                return !string.IsNullOrWhiteSpace(reason);
            }

            return false;
        }

        private void SetEmptyState()
        {
            InputText.Text = "";
            OutputText.Text = "Ready for input...";
            GuardrailIssuePanel.Visibility = Visibility.Collapsed;
            TermPanel.Visibility = Visibility.Collapsed;
            SetWorkflowState(OverlayWorkflowState.Captured);
            InputText.Focus();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settings = new SettingsWindow();
            settings.ShowDialog();
            LoadProfiles();
            RefreshLaunchPhaseBadge();
            RefreshConfidentialityRouteIndicator();
            RefreshDomainAndScopeBadges();
            RefreshGuardrailOverrideAvailability();
        }

        private void RefreshLaunchPhaseBadge()
        {
            if (FindName("LaunchPhaseBadgeText") is System.Windows.Controls.TextBlock badge)
            {
                badge.Text = $"Phase: {_gtmConfigService.GetActiveLaunchPhase()}";
            }
        }

        private void RefreshConfidentialityRouteIndicator()
        {
            if (FindName("ConfidentialityRouteText") is System.Windows.Controls.TextBlock routeText)
            {
                var disclosure = TranslationService.BuildDataHandlingDisclosure(SettingsService.Current);
                routeText.Text = $"Mode: {disclosure.ActiveMode} | Route: {disclosure.ProviderRoute}";
            }

            RefreshLatencySummary();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (IsImeCompositionKey(e))
            {
                return;
            }

            if (e.Key == Key.Escape)
            {
                this.Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.R && _pendingWarningIssues.Count > 0)
            {
                GuardrailIssuePanel.Visibility = Visibility.Visible;
                GuardrailIssueList.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.A && _pendingWarningIssues.Count > 0)
            {
                ApplyPendingWarningPaste();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CopyResultAndClose();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                HandleTranslateSelectionInPlaceFallback();
                e.Handled = true;
            }
        }

        private async void InputText_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (IsImeCompositionKey(e))
            {
                return;
            }

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                await PerformTranslationAsync(InputText.Text);
            }
        }

        private void OutputText_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (IsImeCompositionKey(e))
            {
                return;
            }

            if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None && _workflowController.CurrentState == OverlayWorkflowState.Ready)
            {
                e.Handled = true;
                _pendingApplyInPlace = false;
                CopyResultAndClose();
            }
        }

        private void RecordLatencySample(
            DateTime capturedAtUtc,
            DateTime requestStartedUtc,
            DateTime responseReceivedUtc,
            DateTime renderCompletedUtc,
            string sourceText,
            TranslationExecutionResult execution)
        {
            double captureToRequestStartMs = Math.Max(0, (requestStartedUtc - capturedAtUtc).TotalMilliseconds);
            double providerRoundtripMs = execution.ProviderRoundtripMs > 0
                ? execution.ProviderRoundtripMs
                : Math.Max(0, (responseReceivedUtc - requestStartedUtc).TotalMilliseconds);
            double responseToRenderMs = Math.Max(0, (renderCompletedUtc - responseReceivedUtc).TotalMilliseconds);
            double endToEndMs = Math.Max(0, (renderCompletedUtc - capturedAtUtc).TotalMilliseconds);

            ReflexLatencyMetricsService.Instance.Record(new ReflexLatencySample
            {
                IsShortSegment = execution.IsShortSegmentMode,
                SourceLength = sourceText?.Length ?? 0,
                CaptureToRequestStartMs = captureToRequestStartMs,
                ProviderRoundtripMs = providerRoundtripMs,
                ResponseToRenderMs = responseToRenderMs,
                EndToEndMs = endToEndMs,
                ProviderUsed = execution.ProviderUsed ?? string.Empty,
                UsedFallbackProvider = execution.UsedFallbackProvider,
                BudgetEnforced = execution.BudgetEnforced,
                BudgetExceeded = execution.BudgetExceeded
            });

            RefreshLatencySummary();
        }

        private void RefreshLatencySummary()
        {
            if (FindName("LatencySummaryText") is not System.Windows.Controls.TextBlock summaryText)
            {
                return;
            }

            ReflexLatencySnapshot snapshot = ReflexLatencyMetricsService.Instance.GetSnapshot();
            summaryText.Text = $"Latency p50/p95: {snapshot.EndToEndP50Ms:F0}/{snapshot.EndToEndP95Ms:F0}ms ({snapshot.ShortSegmentSampleCount} short)";

            if (FindName("LatencyWarningText") is System.Windows.Controls.TextBlock warningText)
            {
                bool shouldWarn = snapshot.ShortSegmentSampleCount > 0 && snapshot.EndToEndP95Ms > ShortSegmentP95ThresholdMs;
                warningText.Visibility = shouldWarn ? Visibility.Visible : Visibility.Collapsed;
                warningText.Text = shouldWarn
                    ? $"Warning: short-segment p95 {snapshot.EndToEndP95Ms:F0}ms exceeds {ShortSegmentP95ThresholdMs:F0}ms target."
                    : string.Empty;
            }
        }

        private void RefreshGuardrailOverrideAvailability()
        {
            bool allowed = SettingsService.Current.AllowGuardrailOverrides;
            OverrideBlockedIssuesButton.Visibility = allowed ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyWarningsAnyway_Click(object sender, RoutedEventArgs e)
        {
            ApplyPendingWarningPaste();
        }

        private void ApplyPendingWarningPaste()
        {
            if (_pendingWarningIssues == null || _pendingWarningIssues.Count == 0)
            {
                return;
            }

            ExecuteSafePasteAndClose(
                _pendingWarningOutput,
                _pendingWarningPasteInHostApp,
                _pendingWarningExpectedClipboardSnapshot);
            GuardrailIssuePanel.Visibility = Visibility.Collapsed;
            _pendingWarningIssues = Array.Empty<GuardrailResult>();
        }

        private static bool IsImeCompositionKey(System.Windows.Input.KeyEventArgs e)
        {
            return e.ImeProcessedKey != Key.None || e.Key == Key.ImeProcessed || e.Key == Key.DeadCharProcessed;
        }

        protected override void OnClosed(EventArgs e)
        {
            (_gtmConfigService as IDisposable)?.Dispose();
            (_telemetryService as IDisposable)?.Dispose();
            base.OnClosed(e);
        }
    }
}
