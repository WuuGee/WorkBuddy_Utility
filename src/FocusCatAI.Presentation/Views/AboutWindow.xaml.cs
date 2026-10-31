using System.Windows;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Shared;

namespace FocusCatAI.Presentation.Views;

public partial class AboutWindow : Window
{
    private readonly IExternalLauncher _launcher;

    public AboutWindow(IAppPaths paths, IExternalLauncher launcher)
    {
        InitializeComponent();
        _launcher = launcher;
        VersionText.Text = $"Version {AppInfo.CurrentVersion}";
        CopyrightText.Text = AppInfo.Copyright;
        LicenseText.Text = AppInfo.LicenseSummary;
        RepositoryText.Text = AppInfo.RepositoryUrl;
        DataFolderText.Text = paths.RootDirectory;
        NoticeText.Text = AppInfo.OpenSourceNotice;
    }

    private void OnOpenGitHub(object sender, RoutedEventArgs e) => _launcher.OpenUrl(AppInfo.RepositoryUrl);

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
