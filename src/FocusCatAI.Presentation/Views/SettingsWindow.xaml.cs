using System.Windows;
using System.Windows.Controls;
using FocusCatAI.Presentation.ViewModels;

namespace FocusCatAI.Presentation.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly FrameworkElement[] _pages;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        VersionLabel.Text = viewModel.VersionText;
        _pages = [PageGeneral, PagePet, PageBehavior, PageQuiet, PageNotifications, PageAdvanced];
        Closed += (_, _) => viewModel.Dispose();
    }

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pages is null)
        {
            return;
        }

        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visibility = i == Categories.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnOpenData(object sender, RoutedEventArgs e) => _viewModel.OpenDataFolder();

    private void OnOpenLogs(object sender, RoutedEventArgs e) => _viewModel.OpenLogFolder();

    private void OnResetPreferences(object sender, RoutedEventArgs e)
    {
        if (Confirm("Reset the pet's favorite spots to their defaults?"))
        {
            _viewModel.ResetPreferences();
        }
    }

    private void OnResetAll(object sender, RoutedEventArgs e)
    {
        if (Confirm("Reset every setting to its default?"))
        {
            _viewModel.ResetAllSettings();
        }
    }

    private bool Confirm(string question) =>
        MessageBox.Show(this, question, "WorkBuddy", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
