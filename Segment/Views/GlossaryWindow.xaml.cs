using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Services;

// Hiçbir System.Windows veya System.Windows.Forms using'i KULLANMAYIN

namespace Segment.App.Views
{
    public class GlossaryDisplayItem
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public int UsageCount { get; set; }
        public string Scope { get; set; }
    }

    public partial class GlossaryWindow : System.Windows.Window
    {
        private List<GlossaryDisplayItem> _allItems = new();
        private bool _isGlobalTab = false;

        public GlossaryWindow()
        {
            InitializeComponent();
            this.MouseLeftButtonDown += (s, e) => { try { this.DragMove(); } catch { } };
            RefreshData();
        }

        private void RefreshData()
        {
            _allItems.Clear();

            var profile = _isGlobalTab ? GlossaryService.GlobalProfile : GlossaryService.CurrentProfile;

            if (profile == null) return;

            if (ScopeTabs.Items.Count > 0)
            {
                var currentTab = ScopeTabs.Items[0] as System.Windows.Controls.TabItem;
                if (!_isGlobalTab && currentTab != null)
                    currentTab.Header = $"Project ({profile.Name})";
            }

            foreach (var entry in profile.Terms.FindAll())
            {
                _allItems.Add(new GlossaryDisplayItem
                {
                    Source = entry.Source,
                    Target = entry.Target,
                    UsageCount = entry.UsageCount,
                    Scope = _isGlobalTab ? "Global" : "Project"
                });
            }

            _allItems.Reverse();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string filter = SearchBox.Text.Trim();

            var filtered = string.IsNullOrEmpty(filter)
                ? _allItems
                : _allItems.Where(x => x.Source.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                       x.Target.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            TermsGrid.ItemsSource = filtered;
            TotalCountText.Text = $"{filtered.Count} terms found";

            if (SearchPlaceholder != null)
            {
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(filter)
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
        }

        private void ScopeTabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.Source is System.Windows.Controls.TabControl)
            {
                var tab = ScopeTabs.SelectedItem as System.Windows.Controls.TabItem;
                if (tab?.Tag == null) return;

                bool newMode = (tab.Tag.ToString() == "Global");

                if (_isGlobalTab != newMode)
                {
                    _isGlobalTab = newMode;
                    RefreshData();
                }
            }
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void DeleteTerm_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is GlossaryDisplayItem item)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete '{item.Source} -> {item.Target}'?",
                    "Delete Term",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    GlossaryService.RemoveTerm(item.Source, _isGlobalTab);
                    RefreshData();
                }
            }
        }

        private void Close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }
    }
}