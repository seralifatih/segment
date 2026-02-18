using System;
using System.Windows;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.App.Views
{
    public partial class LearningWidget : Window
    {
        private DetectedChange _change;
        private string _srcLang = "English";
        private string _trgLang = "Turkish";

        public LearningWidget(DetectedChange change)
        {
            InitializeComponent();
            _change = change;

            var settings = SettingsService.Current;
            _trgLang = settings.TargetLanguage ?? "Turkish";
            _srcLang = "English";

            SourceLangLabel.Text = $"{_srcLang} (Source)";
            TargetLangLabel.Text = $"{_trgLang} (Target)";

            OldTermBox.Text = change.SourceTerm;
            NewTermBox.Text = change.NewTerm;

            ProjectNameText.Text = $"({GlossaryService.CurrentProfile.Name})";

            PositionWindow();
            AnimateEntry();
            AutoLemmatize();
        }

        private async void AutoLemmatize()
        {
            OldTermBox.Opacity = 0.5;
            NewTermBox.Opacity = 0.5;

            var result = await LemmaService.AlignAndLemmatizeAsync(
                _change.FullSourceText,
                _change.SourceTerm,
                _change.NewTerm,
                _srcLang,
                _trgLang);

            OldTermBox.Text = PromptSafetySanitizer.SanitizeGlossaryConstraint(result.SourceLemma);
            NewTermBox.Text = PromptSafetySanitizer.SanitizeGlossaryConstraint(result.TargetLemma);

            OldTermBox.Opacity = 1;
            NewTermBox.Opacity = 1;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string source = PromptSafetySanitizer.SanitizeGlossaryConstraint(OldTermBox.Text);
            string target = PromptSafetySanitizer.SanitizeGlossaryConstraint(NewTermBox.Text);

            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
            {
                if (PromptSafetySanitizer.IsInstructionLike(source) || PromptSafetySanitizer.IsInstructionLike(target))
                {
                    System.Windows.MessageBox.Show(
                        "Term candidate includes instruction-like payload and was blocked.",
                        "Prompt Safety",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                bool isGlobal = ScopeGlobal.IsChecked == true;
                bool isProjectFrozen = !isGlobal && GlossaryService.CurrentProfile?.IsFrozen == true;
                bool guardrailOverrideEnabled = SettingsService.Current.AllowGuardrailOverrides;
                if (isGlobal && SettingsService.Current.RequireExplicitSharedPromotionApproval)
                {
                    if (!TryPromptSharedPromotionApproval(out string approvalReason))
                    {
                        ComplianceAuditService.Default.Record(new ComplianceAuditRecord
                        {
                            EventType = ComplianceAuditEventType.GlossaryConflictDecision,
                            AccountId = SettingsService.Current.AccountId,
                            Decision = "shared_promotion_denied",
                            ActiveMode = SettingsService.Current.ConfidentialProjectLocalOnly ? "Confidential Local-Only" : "Standard",
                            ProviderRoute = SettingsService.Current.AiProvider,
                            RetentionPolicySummary = SettingsService.Current.RetentionPolicySummary,
                            Details = $"Shared glossary promotion denied for '{source}'. Explicit approval missing.",
                            Metadata = new()
                            {
                                ["scope"] = "global",
                                ["source"] = source
                            }
                        });
                        return;
                    }

                    ComplianceAuditService.Default.Record(new ComplianceAuditRecord
                    {
                        EventType = ComplianceAuditEventType.GlossaryConflictDecision,
                        AccountId = SettingsService.Current.AccountId,
                        Decision = "shared_promotion_approved",
                        ActiveMode = SettingsService.Current.ConfidentialProjectLocalOnly ? "Confidential Local-Only" : "Standard",
                        ProviderRoute = SettingsService.Current.AiProvider,
                        RetentionPolicySummary = SettingsService.Current.RetentionPolicySummary,
                        Details = $"Shared glossary promotion approved for '{source}'. Reason: {approvalReason}",
                        Metadata = new()
                        {
                            ["scope"] = "global",
                            ["source"] = source
                        }
                    });
                }

                if (isProjectFrozen && !guardrailOverrideEnabled)
                {
                    ComplianceAuditService.Default.Record(new ComplianceAuditRecord
                    {
                        EventType = ComplianceAuditEventType.GuardrailOverride,
                        AccountId = SettingsService.Current.AccountId,
                        Decision = "blocked",
                        ActiveMode = SettingsService.Current.ConfidentialProjectLocalOnly ? "Confidential Local-Only" : "Standard",
                        ProviderRoute = SettingsService.Current.AiProvider,
                        RetentionPolicySummary = SettingsService.Current.RetentionPolicySummary,
                        Details = $"Attempted update on frozen profile '{GlossaryService.CurrentProfile.Name}' was blocked.",
                        Metadata = new()
                        {
                            ["profile"] = GlossaryService.CurrentProfile.Name,
                            ["term"] = source
                        }
                    });

                    System.Windows.MessageBox.Show(
                        "This glossary profile is frozen. Guardrail overrides are disabled for this account.",
                        "Guardrail Blocked",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var targetProfile = isGlobal ? GlossaryService.GlobalProfile : GlossaryService.CurrentProfile;
                var existing = targetProfile.Terms.FindById(source);
                bool exists = existing != null;

                if (exists)
                {
                    var oldVal = existing.Target;

                    if (oldVal.Equals(target, StringComparison.OrdinalIgnoreCase))
                    {
                        Close();
                        return;
                    }

                    var result = System.Windows.MessageBox.Show(
                        $"'{source}' is already defined as '{oldVal}' in {(isGlobal ? "Global" : "Project")} scope.\n\nOverwrite with '{target}'?",
                        "Conflict Detected",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        ComplianceAuditService.Default.Record(new ComplianceAuditRecord
                        {
                            EventType = ComplianceAuditEventType.GlossaryConflictDecision,
                            AccountId = SettingsService.Current.AccountId,
                            Decision = "skip_overwrite",
                            ActiveMode = SettingsService.Current.ConfidentialProjectLocalOnly ? "Confidential Local-Only" : "Standard",
                            ProviderRoute = SettingsService.Current.AiProvider,
                            RetentionPolicySummary = SettingsService.Current.RetentionPolicySummary,
                            Details = $"User skipped overwrite for '{source}' in {(isGlobal ? "global" : "project")} scope.",
                            Metadata = new()
                            {
                                ["term"] = source,
                                ["existing_target"] = oldVal,
                                ["requested_target"] = target,
                                ["scope"] = isGlobal ? "global" : "project"
                            }
                        });
                        return;
                    }

                    ComplianceAuditService.Default.Record(new ComplianceAuditRecord
                    {
                        EventType = ComplianceAuditEventType.GlossaryConflictDecision,
                        AccountId = SettingsService.Current.AccountId,
                        Decision = "confirm_overwrite",
                        ActiveMode = SettingsService.Current.ConfidentialProjectLocalOnly ? "Confidential Local-Only" : "Standard",
                        ProviderRoute = SettingsService.Current.AiProvider,
                        RetentionPolicySummary = SettingsService.Current.RetentionPolicySummary,
                        Details = $"User confirmed overwrite for '{source}' in {(isGlobal ? "global" : "project")} scope.",
                        Metadata = new()
                        {
                            ["term"] = source,
                            ["existing_target"] = oldVal,
                            ["requested_target"] = target,
                            ["scope"] = isGlobal ? "global" : "project"
                        }
                    });
                }

                if (isProjectFrozen && guardrailOverrideEnabled)
                {
                    ComplianceAuditService.Default.Record(new ComplianceAuditRecord
                    {
                        EventType = ComplianceAuditEventType.GuardrailOverride,
                        AccountId = SettingsService.Current.AccountId,
                        Decision = "applied",
                        ActiveMode = SettingsService.Current.ConfidentialProjectLocalOnly ? "Confidential Local-Only" : "Standard",
                        ProviderRoute = SettingsService.Current.AiProvider,
                        RetentionPolicySummary = SettingsService.Current.RetentionPolicySummary,
                        Details = $"Guardrail override used on frozen profile '{GlossaryService.CurrentProfile.Name}'.",
                        Metadata = new()
                        {
                            ["profile"] = GlossaryService.CurrentProfile.Name,
                            ["term"] = source
                        }
                    });
                }

                GlossaryService.AddTerm(source, target, isGlobal);
            }

            Close();
        }

        private static bool TryPromptSharedPromotionApproval(out string reason)
        {
            reason = string.Empty;
            MessageBoxResult confirm = System.Windows.MessageBox.Show(
                "Promoting this term to Global/Shared scope affects broader users.\n\nDo you want to continue?",
                "Shared Glossary Promotion",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return false;
            }

            var dialog = new Window
            {
                Title = "Shared Promotion Approval",
                Width = 460,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow
            };

            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Provide a short approval reason for shared promotion:",
                TextWrapping = TextWrapping.Wrap
            });

            var reasonBox = new System.Windows.Controls.TextBox
            {
                Margin = new Thickness(0, 10, 0, 10),
                AcceptsReturn = true,
                Height = 80,
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(reasonBox);

            var buttons = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            var cancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
            var ok = new System.Windows.Controls.Button { Content = "Approve", Width = 90 };
            cancel.Click += (_, _) => { dialog.DialogResult = false; dialog.Close(); };
            ok.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(reasonBox.Text))
                {
                    System.Windows.MessageBox.Show("Approval reason is required.", "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                dialog.DialogResult = true;
                dialog.Close();
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            panel.Children.Add(buttons);
            dialog.Content = panel;

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                reason = reasonBox.Text.Trim();
                return true;
            }

            return false;
        }

        private void Ignore_Click(object sender, RoutedEventArgs e) => Close();

        private void PositionWindow()
        {
            var desktop = SystemParameters.WorkArea;
            Left = desktop.Right - Width - 20;
            Top = desktop.Bottom - Height - 20;
        }

        private void AnimateEntry()
        {
            Opacity = 0;
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromSeconds(0.3));
            BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }
}
