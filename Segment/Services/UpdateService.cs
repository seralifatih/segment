using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using WpfApplication = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace Segment.Services
{
    public class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }

    public interface IUpdateService
    {
        Task CheckForUpdatesAsync();
    }

    public class UpdateService : IUpdateService
    {
        private const string RemoteVersionUrl = "https://raw.githubusercontent.com/seralifatih/segment/main/version.json";
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        
        public async Task CheckForUpdatesAsync()
        {
            try
            {
                // 1. Fetch JSON
                var json = await HttpClient.GetStringAsync(RemoteVersionUrl);
                var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (updateInfo == null || string.IsNullOrWhiteSpace(updateInfo.Version)) return;

                // 2. Compare Versions
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVersion == null) return;

                if (!Version.TryParse(updateInfo.Version, out var remoteVersion)) return;

                if (remoteVersion > currentVersion)
                {
                    // 3. Notify User (on UI Thread)
                    WpfApplication.Current.Dispatcher.Invoke(() => 
                    {
                        ShowUpdateNotification(updateInfo.Version, updateInfo.DownloadUrl);
                    });
                }
            }
            catch (Exception ex)
            {
                // Silently fail - never annoy the user if internet is down
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        private void ShowUpdateNotification(string newVersion, string url)
        {
            // Simple MessageBox for now, or use your Toast system if preferred
            var result = MessageBox.Show(
                $"A new version of Segment is available (v{newVersion}).\n\nWould you like to download it now?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                try 
                { 
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); 
                }
                catch { /* Ignore browser errors */ }
            }
        }
    }
}
