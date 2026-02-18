using System.Windows;
using System.Windows.Input;
using Segment.App.Models;

namespace Segment.App.Views
{
    public partial class TermConflictAssistantWindow : Window
    {
        public LearningConflictDecision Decision { get; private set; } = LearningConflictDecision.Cancel;

        public TermConflictAssistantWindow(LearningConflictPrompt prompt)
        {
            InitializeComponent();
            SummaryText.Text = $"{prompt.SourceTerm} ({(prompt.IsGlobalScope ? "Global" : "Project")} scope) already has a preferred translation.";
            ExistingText.Text = prompt.ExistingTarget;
            NewText.Text = prompt.NewTarget;
        }

        private void KeepExisting_Click(object sender, RoutedEventArgs e)
        {
            Decision = LearningConflictDecision.KeepExisting;
            DialogResult = true;
            Close();
        }

        private void UseNew_Click(object sender, RoutedEventArgs e)
        {
            Decision = LearningConflictDecision.UseNewSuggestion;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Decision = LearningConflictDecision.Cancel;
            DialogResult = false;
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                KeepExisting_Click(sender, e);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                UseNew_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
