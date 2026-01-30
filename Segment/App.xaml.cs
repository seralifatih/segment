using System;
using System.IO;
using System.Drawing; // Bitmap, Graphics, Icon için
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms; // NotifyIcon için
using System.Linq;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

using NHotkey;
using NHotkey.Wpf;
using Segment.Views;
using Segment.Services;

namespace Segment
{
    public partial class App : Application
    {
        private FloatingPanel? panel;
        private NotifyIcon? _notifyIcon;
        private readonly string _crashLogPath;
        private Mutex? _singleInstanceMutex;

        // Desteklediğimiz diller listesi
        private readonly string[] commonLanguages = { "Turkish", "English", "German", "French", "Spanish", "Russian", "Japanese" };

        public App()
        {
            // Setup crash log path
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SegmentApp");
            Directory.CreateDirectory(appDataPath);
            _crashLogPath = Path.Combine(appDataPath, "crash_log.txt");

            // Subscribe to global exception handlers (Black Box)
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ========================================
            // TASK 1: Single Instance Lock (Mutex)
            // ========================================
            const string mutexName = "Global\\SegmentAppLock";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show(
                    "Segment is already running in the System Tray.",
                    "Segment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                Shutdown();
                return;
            }

            // ========================================
            // TASK 4: Set ShutdownMode to OnExplicitShutdown
            // ========================================
            // This prevents the app from closing when the welcome window closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Load settings and initialize services
            SettingsService.Load(); // Veya SettingsService.Instance.Load()
            SetupTrayIcon();

            // Register global hotkey
            try
            {
                HotkeyManager.Current.AddOrReplace("ShowSegment", System.Windows.Input.Key.Space, System.Windows.Input.ModifierKeys.Control, OnHotkeyPressed);
            }
            catch { }

            // ========================================
            // TASK 4: First Run Experience
            // ========================================
            if (SettingsService.Instance.IsFirstRun)
            {
                WelcomeWindow welcomeWindow = new WelcomeWindow();
                welcomeWindow.Show();
            }
            // If not first run, app starts silently in system tray
        }

        #region Black Box - Crash Logging

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception, "DispatcherUnhandledException");
            e.Handled = false; // Let the app crash after logging
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogCrash(e.Exception, "UnobservedTaskException");
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash(ex, "AppDomainUnhandledException");
            }
        }

        private void LogCrash(Exception ex, string source)
        {
            try
            {
                string crashDetails = $"""
                    ═══════════════════════════════════════════════════════════
                    CRASH REPORT
                    ═══════════════════════════════════════════════════════════
                    Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    Source:    {source}
                    Message:   {ex.Message}
                    
                    Stack Trace:
                    {ex.StackTrace}
                    
                    Inner Exception:
                    {(ex.InnerException != null ? ex.InnerException.ToString() : "None")}
                    ═══════════════════════════════════════════════════════════
                    
                    """;

                File.AppendAllText(_crashLogPath, crashDetails);

                // Try to show a balloon tip notification before crash
                _notifyIcon?.ShowBalloonTip(
                    3000,
                    "Segment Crashed",
                    "An error occurred. Details saved to crash_log.txt",
                    ToolTipIcon.Error
                );
            }
            catch
            {
                // If logging fails, we can't do much about it
            }
        }

        #endregion

        #region Procedural Tray Icon

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = GeneratePlaceholderIcon();
            _notifyIcon.Text = "Segment (Ctrl+Space)";
            _notifyIcon.Visible = true;

            // Menüyü oluştur
            RefreshTrayMenu();

            // Çift tık -> Ayarlar
            _notifyIcon.DoubleClick += (s, e) => OpenSettings();
        }

        private Icon GeneratePlaceholderIcon()
        {
            using (Bitmap bitmap = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // Draw a purple background
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(138, 43, 226))) // BlueViolet
                {
                    g.FillRectangle(bgBrush, 0, 0, 16, 16);
                }

                // Draw a white box/square in the center to represent "Segment"
                using (SolidBrush fgBrush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(fgBrush, 4, 4, 8, 8);
                }

                // Draw a simple border
                using (Pen borderPen = new Pen(Color.FromArgb(100, 30, 180), 1))
                {
                    g.DrawRectangle(borderPen, 0, 0, 15, 15);
                }

                // Convert bitmap to Icon
                IntPtr hIcon = bitmap.GetHicon();
                Icon icon = Icon.FromHandle(hIcon);
                
                // Note: We return the icon but don't dispose it here
                // The icon will be disposed when the NotifyIcon is disposed
                return icon;
            }
        }

        #endregion

        // Menüyü dinamik olarak oluşturuyoruz ki dil değişince tik işareti güncellensin
        public void RefreshTrayMenu()
        {
            if (_notifyIcon == null) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            string currentLang = SettingsService.Instance.TargetLanguage;

            // --- 1. TARGET LANGUAGE MENU ---
            ToolStripMenuItem langMenu = new ToolStripMenuItem("Target Language");

            // ÖZEL DURUM: Eğer kullanıcının seçtiği dil standart listede yoksa,
            // onu en başa 'Custom' olarak ekle.
            bool isCustom = !commonLanguages.Contains(currentLang);

            if (isCustom && !string.IsNullOrEmpty(currentLang))
            {
                ToolStripMenuItem customItem = new ToolStripMenuItem($"Currently: {currentLang}");
                customItem.Checked = true;
                // Tıklayınca bir şey yapmasına gerek yok, zaten seçili.
                langMenu.DropDownItems.Add(customItem);
                langMenu.DropDownItems.Add(new ToolStripSeparator());
            }

            // Standart Listeyi Döngüyle Ekle
            foreach (var lang in commonLanguages)
            {
                ToolStripMenuItem langItem = new ToolStripMenuItem(lang);

                // Eğer standart dillerden biri seçiliyse tik koy
                if (currentLang == lang)
                {
                    langItem.Checked = true;
                }

                langItem.Click += (s, e) => ChangeLanguageFromTray(lang);
                langMenu.DropDownItems.Add(langItem);
            }

            menu.Items.Add(langMenu);

            // --- SEPARATOR ---
            menu.Items.Add(new ToolStripSeparator());

            // --- 2. SETTINGS ---
            ToolStripMenuItem settingsItem = new ToolStripMenuItem("Settings Window...");
            settingsItem.Click += (s, e) => OpenSettings();
            menu.Items.Add(settingsItem);

            // --- 3. EXIT ---
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ShutdownApp();
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void ChangeLanguageFromTray(string newLang)
        {
            // 1. Ayarı güncelle
            SettingsService.Instance.TargetLanguage = newLang;
            SettingsService.Save();

            // 2. Menüyü yenile (Tik işaretini güncellemek için)
            RefreshTrayMenu();
        }

        private void OpenSettings()
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is SettingsWindow)
                {
                    win.Activate();
                    return;
                }
            }

            SettingsWindow settingsWin = new SettingsWindow();

            // Ayarlar penceresi kapandığında Tray menüsünü güncelle
            // (Çünkü kullanıcı pencereden dili değiştirmiş olabilir)
            settingsWin.Closed += (s, e) => RefreshTrayMenu();

            settingsWin.Show();
        }

        private void ShutdownApp()
        {
            _notifyIcon?.Dispose();
            Shutdown();
        }

        private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            e.Handled = true;
            if (panel == null)
            {
                panel = new FloatingPanel();
                panel.Closed += (s, args) => panel = null;
            }
            panel.HandleSmartHotkey();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}