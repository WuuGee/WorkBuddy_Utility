using System.Diagnostics;
using System.Windows;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Shared;

namespace FocusCatAI.Presentation.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(IAppPaths paths)
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppInfo.CurrentVersion}";
        CopyrightText.Text = AppInfo.Copyright;
        LicenseText.Text = AppInfo.LicenseSummary;
        RepositoryText.Text = AppInfo.RepositoryUrl;
        DataFolderText.Text = paths.RootDirectory;
        NoticeText.Text = AppInfo.OpenSourceNotice;
    }

    private void OnOpenGitHub(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl) { UseShellExecute = true });

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
