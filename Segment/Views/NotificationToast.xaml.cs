using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Segment.App.Models;
using Segment.App.Services;

namespace Segment.App.Views
{
    public partial class NotificationToast : Window
    {
        private readonly DetectedChange _change;
        private readonly DispatcherTimer _autoCloseTimer;
        private readonly ILearningConsentService _consentService;

        public NotificationToast(DetectedChange change)
            : this(change, new LearningConsentService())
        {
        }

        internal NotificationToast(DetectedChange change, ILearningConsentService consentService)
        {
            InitializeComponent();
            _change = change;
            _consentService = consentService;

            OldTermText.Text = change.OldTerm;
            NewTermText.Text = change.NewTerm;

            PositionWindow();

            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(8)
            };
            _autoCloseTimer.Tick += AutoCloseTimer_Tick;
            _autoCloseTimer.Start();

            MouseEnter += (s, e) => _autoCloseTimer.Stop();
            MouseLeave += (s, e) => _autoCloseTimer.Start();
            Loaded += (s, e) => AlwaysButton.Focus();
        }

        private void PositionWindow()
        {
            var desktop = SystemParameters.WorkArea;
            Left = desktop.Right - Width - 20;
            Top = desktop.Bottom - Height - 20;
        }

        private void AutoCloseTimer_Tick(object? sender, EventArgs e)
        {
            _autoCloseTimer.Stop();
            Close();
        }

        private void Always_Click(object sender, RoutedEventArgs e) => ApplyDecision(LearningConsentOption.Always);

        private void Project_Click(object sender, RoutedEventArgs e) => ApplyDecision(LearningConsentOption.ThisProject);

        private void NotNow_Click(object sender, RoutedEventArgs e) => Close();

        private void ApplyDecision(LearningConsentOption option)
        {
            _autoCloseTimer.Stop();
            var outcome = _consentService.ApplyDecision(_change, option, ResolveConflict);
            if (outcome.RequiresConflictResolution)
            {
                System.Windows.MessageBox.Show(
                    "A conflicting preferred translation already exists. Review choices to continue.",
                    "Conflict Detected",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }

            Close();
        }

        private LearningConflictDecision ResolveConflict(LearningConflictPrompt prompt)
        {
            var dialog = new TermConflictAssistantWindow(prompt)
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();
            if (result != true)
            {
                return LearningConflictDecision.Cancel;
            }

            return dialog.Decision;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.A)
            {
                ApplyDecision(LearningConsentOption.Always);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.P)
            {
                ApplyDecision(LearningConsentOption.ThisProject);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N || e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
