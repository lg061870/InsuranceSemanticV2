using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ConversaCore.TopicTool.Models;
using ConversaCore.TopicTool.Services;

namespace ConversaCore.TopicTool
{
    public partial class TopicManagementControl : UserControl
    {
        private string? _topicsRootFolder;

        private enum PanelViewMode
        {
            Normal,
            DialogMaximized,
            StructureMaximized,
            ValidationMaximized
        }

        private PanelViewMode _currentViewMode = PanelViewMode.Normal;

        public TopicManagementControl()
        {
            InitializeComponent();
            ApplyLayout();
        }

        public void SetTopicsRoot(string topicsFolder)
        {
            _topicsRootFolder = topicsFolder;
        }

        private void OnDialogPanelToggleClicked(object sender, RoutedEventArgs e)
        {
            _currentViewMode = _currentViewMode == PanelViewMode.DialogMaximized
                ? PanelViewMode.Normal
                : PanelViewMode.DialogMaximized;
            ApplyLayout();
        }

        private void OnStructurePanelToggleClicked(object sender, RoutedEventArgs e)
        {
            _currentViewMode = _currentViewMode == PanelViewMode.StructureMaximized
                ? PanelViewMode.Normal
                : PanelViewMode.StructureMaximized;
            ApplyLayout();
        }

        private void OnValidationPanelToggleClicked(object sender, RoutedEventArgs e)
        {
            _currentViewMode = _currentViewMode == PanelViewMode.ValidationMaximized
                ? PanelViewMode.Normal
                : PanelViewMode.ValidationMaximized;
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (RootLayoutGrid == null || MainSplitGrid == null)
            {
                return;
            }

            // Default layout
            var row0 = RootLayoutGrid.RowDefinitions[0];
            var row1 = RootLayoutGrid.RowDefinitions[1];
            var row2 = RootLayoutGrid.RowDefinitions[2];

            var col0 = MainSplitGrid.ColumnDefinitions[0];
            var col1 = MainSplitGrid.ColumnDefinitions[1];
            var col2 = MainSplitGrid.ColumnDefinitions[2];

            // Reset visibility
            DialogPanel.Visibility = Visibility.Visible;
            StructurePanel.Visibility = Visibility.Visible;
            ValidationPanel.Visibility = Visibility.Visible;
            MainSplitter.Visibility = Visibility.Visible;

            switch (_currentViewMode)
            {
                case PanelViewMode.DialogMaximized:
                    row0.Height = new GridLength(0);
                    row1.Height = new GridLength(1, GridUnitType.Star);
                    row2.Height = new GridLength(0);

                    col0.Width = new GridLength(1, GridUnitType.Star);
                    col1.Width = new GridLength(0);
                    col2.Width = new GridLength(0);

                    StructurePanel.Visibility = Visibility.Collapsed;
                    ValidationPanel.Visibility = Visibility.Collapsed;
                    MainSplitter.Visibility = Visibility.Collapsed;
                    break;

                case PanelViewMode.StructureMaximized:
                    row0.Height = new GridLength(0);
                    row1.Height = new GridLength(1, GridUnitType.Star);
                    row2.Height = new GridLength(0);

                    col0.Width = new GridLength(0);
                    col1.Width = new GridLength(0);
                    col2.Width = new GridLength(1, GridUnitType.Star);

                    DialogPanel.Visibility = Visibility.Collapsed;
                    ValidationPanel.Visibility = Visibility.Collapsed;
                    MainSplitter.Visibility = Visibility.Collapsed;
                    break;

                case PanelViewMode.ValidationMaximized:
                    row0.Height = new GridLength(0);
                    row1.Height = new GridLength(0);
                    row2.Height = new GridLength(1, GridUnitType.Star);

                    col0.Width = new GridLength(1, GridUnitType.Star);
                    col1.Width = new GridLength(0);
                    col2.Width = new GridLength(0);

                    DialogPanel.Visibility = Visibility.Collapsed;
                    StructurePanel.Visibility = Visibility.Collapsed;
                    MainSplitter.Visibility = Visibility.Collapsed;
                    break;

                default:
                    // Normal
                    row0.Height = GridLength.Auto;
                    row1.Height = new GridLength(1, GridUnitType.Star);
                    row2.Height = new GridLength(200);

                    col0.Width = new GridLength(2, GridUnitType.Star);
                    col1.Width = new GridLength(5);
                    col2.Width = new GridLength(3, GridUnitType.Star);
                    break;
            }
        }

        private void OnGeneratePlanClicked(object sender, RoutedEventArgs e)
        {
            // TODO: Wire AI plan generation.
        }

        private void OnRefineClicked(object sender, RoutedEventArgs e)
        {
            // TODO: Wire AI refinement.
        }

        private void OnAskAiQuestionsClicked(object sender, RoutedEventArgs e)
        {
            // TODO: Wire AI clarification questions.
        }

        private void OnValidateClicked(object sender, RoutedEventArgs e)
        {
            // TODO: Run design-time validation and populate MessagesListView.
        }

        private void OnGenerateCodeClicked(object sender, RoutedEventArgs e)
        {
            // TODO: Confirm destructive operation and call TopicCodeGenerator for each designed topic.
        }
    }
}
