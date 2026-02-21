using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ConversaCore.TopicDesigner.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private enum PanelLayoutState
    {
        Normal,
        LeftMaximized,
        RightMaximized,
        BottomMaximized
    }

    private PanelLayoutState _panelLayoutState = PanelLayoutState.Normal;

    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void OnBrowseTopicsFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Forms.FolderBrowserDialog {
            Description = "Select the Topics folder to generate code into."
        };

        var result = dialog.ShowDialog();
        if (result == Forms.DialogResult.OK)
        {
            TopicsFolderTextBox.Text = dialog.SelectedPath;
            TryPopulateDialogDefinitionFromTopics(dialog.SelectedPath);
        }
    }

    private void OnGeneratePlanClicked(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Generate Plan is not wired yet. This will call the LLM and build a topic structure.", "TopicDesigner", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnRefineClicked(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Refine is not wired yet. This will refine the existing topic structure.", "TopicDesigner", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnValidateClicked(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Validate is not wired yet. This will run validation rules over the current document.", "TopicDesigner", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnGenerateCodeClicked(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Generate Code is not wired yet. This will emit ConversaCore topics into the selected Topics folder.", "TopicDesigner", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void OnToggleLeftPanelClicked(object sender, RoutedEventArgs e)
    {
        if (_panelLayoutState == PanelLayoutState.LeftMaximized)
        {
            SetNormalLayout();
        }
        else
        {
            SetLeftMaximizedLayout();
        }
    }

    private void OnToggleRightPanelClicked(object sender, RoutedEventArgs e)
    {
        if (_panelLayoutState == PanelLayoutState.RightMaximized)
        {
            SetNormalLayout();
        }
        else
        {
            SetRightMaximizedLayout();
        }
    }

    private void SetNormalLayout()
    {
        if (RootGrid.RowDefinitions.Count >= 4)
        {
            RootGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
            RootGrid.RowDefinitions[3].Height = new GridLength(200);
        }

        if (MainSplitGrid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        MainSplitGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
        MainSplitGrid.ColumnDefinitions[1].Width = new GridLength(5);
        MainSplitGrid.ColumnDefinitions[2].Width = new GridLength(3, GridUnitType.Star);

        DialogDefinitionBorder.Visibility = Visibility.Visible;
        TopicStructureBorder.Visibility = Visibility.Visible;
    MessagesBorder.Visibility = Visibility.Visible;

        _panelLayoutState = PanelLayoutState.Normal;
    }

    private void SetLeftMaximizedLayout()
    {
        if (RootGrid.RowDefinitions.Count >= 4)
        {
            RootGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
            RootGrid.RowDefinitions[3].Height = new GridLength(0);
        }

        if (MainSplitGrid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        MainSplitGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        MainSplitGrid.ColumnDefinitions[1].Width = new GridLength(0);
        MainSplitGrid.ColumnDefinitions[2].Width = new GridLength(0);

        DialogDefinitionBorder.Visibility = Visibility.Visible;
        TopicStructureBorder.Visibility = Visibility.Collapsed;
    MessagesBorder.Visibility = Visibility.Collapsed;

        _panelLayoutState = PanelLayoutState.LeftMaximized;
    }

    private void SetRightMaximizedLayout()
    {
        if (RootGrid.RowDefinitions.Count >= 4)
        {
            RootGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
            RootGrid.RowDefinitions[3].Height = new GridLength(0);
        }

        if (MainSplitGrid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        MainSplitGrid.ColumnDefinitions[0].Width = new GridLength(0);
        MainSplitGrid.ColumnDefinitions[1].Width = new GridLength(0);
        MainSplitGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);

        DialogDefinitionBorder.Visibility = Visibility.Collapsed;
        TopicStructureBorder.Visibility = Visibility.Visible;
    MessagesBorder.Visibility = Visibility.Collapsed;

        _panelLayoutState = PanelLayoutState.RightMaximized;
    }

    private void OnToggleBottomPanelClicked(object sender, RoutedEventArgs e)
    {
        if (_panelLayoutState == PanelLayoutState.BottomMaximized)
        {
            SetNormalLayout();
        }
        else
        {
            SetBottomMaximizedLayout();
        }
    }

    private void SetBottomMaximizedLayout()
    {
        if (RootGrid.RowDefinitions.Count >= 4)
        {
            RootGrid.RowDefinitions[2].Height = new GridLength(0);
            RootGrid.RowDefinitions[3].Height = new GridLength(1, GridUnitType.Star);
        }

        // Keep existing column widths; simply hide main split area via row height.
        DialogDefinitionBorder.Visibility = Visibility.Visible;
        TopicStructureBorder.Visibility = Visibility.Visible;
        MessagesBorder.Visibility = Visibility.Visible;

        _panelLayoutState = PanelLayoutState.BottomMaximized;
    }

    private void TryPopulateDialogDefinitionFromTopics(string topicsFolderPath)
    {
        if (string.IsNullOrWhiteSpace(topicsFolderPath))
        {
            return;
        }

        if (!Directory.Exists(topicsFolderPath))
        {
            return;
        }

        string[] topicFiles;
        try
        {
            topicFiles = Directory.GetFiles(topicsFolderPath, "*Topic.cs", SearchOption.AllDirectories);
        }
        catch
        {
            return;
        }

        if (topicFiles.Length == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Reverse-engineered dialog from existing ConversaCore topics.");
        sb.AppendLine();

        foreach (var file in topicFiles)
        {
            string source;
            try
            {
                source = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            var classNameMatch = Regex.Match(source, @"class\s+(?<name>[A-Za-z0-9_]+Topic)");
            var className = classNameMatch.Success ? classNameMatch.Groups["name"].Value : Path.GetFileNameWithoutExtension(file);

            sb.AppendLine($"Topic: {className}");

            var activityMatches = Regex.Matches(source,
                @"Add\s*\(\s*new\s+(?<type>[A-Za-z0-9_]+)(?<generic><[^>]+>)?\s*\(\s*""(?<id>[^""]+)""");

            if (activityMatches.Count == 0)
            {
                sb.AppendLine("  (No activities detected via Add(new ...).)");
                sb.AppendLine();
                continue;
            }

            int stepIndex = 1;
            foreach (Match match in activityMatches)
            {
                if (!match.Success)
                {
                    continue;
                }

                var type = match.Groups["type"].Value;
                var generic = match.Groups["generic"].Success ? match.Groups["generic"].Value : string.Empty;
                var id = match.Groups["id"].Value;

                var typeDisplay = string.IsNullOrEmpty(generic) ? type : type + generic;
                sb.AppendLine($"  {stepIndex}. Activity '{id}' of type {typeDisplay}.");
                stepIndex++;
            }

            sb.AppendLine();
        }

        if (sb.Length > 0)
        {
            DialogDefinitionTextBox.Text = sb.ToString();
        }
    }
}