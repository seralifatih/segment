using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Segment.App.Services;

namespace Segment.App.Views
{
    public partial class NotificationToast : Window
    {
        private readonly DetectedChange _change;
        private readonly DispatcherTimer _autoCloseTimer;

        public NotificationToast(DetectedChange change)
        {
            InitializeComponent();
            _change = change;

            OldTermText.Text = change.OldTerm;
            NewTermText.Text = change.NewTerm;

            PositionWindow();

            // Setup auto-close timer (5 seconds)
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _autoCloseTimer.Tick += AutoCloseTimer_Tick;
            _autoCloseTimer.Start();

            // Pause timer when user hovers
            this.MouseEnter += (s, e) => _autoCloseTimer.Stop();
            this.MouseLeave += (s, e) => _autoCloseTimer.Start();
        }

        private void PositionWindow()
        {
            var desktop = SystemParameters.WorkArea;
            this.Left = desktop.Right - this.Width - 20;
            this.Top = desktop.Bottom - this.Height - 20;
        }

        private void AutoCloseTimer_Tick(object? sender, EventArgs e)
        {
            _autoCloseTimer.Stop();
            this.Close();
        }

        private void Toast_Click(object sender, MouseButtonEventArgs e)
        {
            _autoCloseTimer.Stop(); // Stop timer when user clicks
            var widget = new LearningWidget(_change);
            widget.Show();
            this.Close();
        }
    }
}
