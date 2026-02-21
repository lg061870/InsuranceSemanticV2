using System.Windows;

namespace ConversaCore.TopicDesigner.App;

public partial class SettingsWindow : Window
{
    private ProviderInfo _currentProvider;

    public SettingsWindow()
    {
        InitializeComponent();
        ProviderComboBox.ItemsSource = ProviderInfo.All;
        ProviderComboBox.DisplayMemberPath = nameof(ProviderInfo.DisplayName);
        ProviderComboBox.SelectedIndex = 0;
    }

    private void OnProviderChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProviderComboBox.SelectedItem is ProviderInfo info)
        {
            _currentProvider = info;
            UpdateProviderDetails();
        }
    }

    private void UpdateProviderDetails()
    {
        if (_currentProvider == null)
        {
            return;
        }

        EnvVarTextBlock.Text = $"Set the environment variable {_currentProvider.ApiKeyEnvVar} with your API key.";
        ExtraHintTextBlock.Text = _currentProvider.ExtraEnvVarHint ?? string.Empty;

        PowershellExampleTextBox.Text = $"$env:{_currentProvider.ApiKeyEnvVar} = \"<your-key-here>\"";
        DocsLinkTextBlock.Text = _currentProvider.DocsUrl;
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
