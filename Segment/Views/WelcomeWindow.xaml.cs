using System.Windows;
using Segment.App.Services;

namespace Segment.App.Views
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Mark first run as complete
            SettingsService.Current.IsFirstRun = false;
            SettingsService.Save();

            // Close the welcome window
            Close();
        }
    }
}
