using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Updates;
using FocusCatAI.Presentation.Animation;
using FocusCatAI.Presentation.Configuration;
using FocusCatAI.Presentation.Views;
using FocusCatAI.Shared;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// The tray icon and its menu: application management only (pet visibility, settings, restart, about, updates, exit).
/// Pomodoro is deliberately not in the tray.
/// </summary>
public sealed class TrayIconService(
    ISpriteManager sprites,
    IOptions<PetOptions> options,
    ISettingsService settings,
    IStartupManager startup,
    IServiceProvider services,
    ILogger<TrayIconService> logger) : IDisposable
{
    public const string ShowCatHeader = "Show Cat";
    public const string HideCatHeader = "Hide Cat";
    public const string OpenSettingsHeader = "Open Settings";
    public const string RestartHeader = "Restart WorkBuddy";
    public const string AboutHeader = "About";
    public const string CheckForUpdatesHeader = "Check for Updates";
    public const string ExitHeader = "Exit";

    private TaskbarIcon? _icon;
    private ContextMenu? _menu;
    private MenuItem? _showItem;
    private MenuItem? _hideItem;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private bool _checkingForUpdates;

    /// <summary>The current tooltip text. Exposed for diagnostics.</summary>
    public string? Tooltip => _icon?.ToolTipText;

    public bool IsVisible => _icon is not null;

    /// <summary>The menu entries in order: header text, or null for a separator. Exposed for diagnostics.</summary>
    public IReadOnlyList<string?> MenuHeaders =>
        _menu?.Items.Cast<object>().Select(i => (i as MenuItem)?.Header as string).ToList() ?? [];

    /// <summary>Returns the real menu item with this header so diagnostics can click exactly what a user would.</summary>
    public MenuItem? FindMenuItem(string header) =>
        _menu?.Items.OfType<MenuItem>().FirstOrDefault(i => i.Header as string == header);

    public void Show()
    {
        var skin = sprites.Load(options.Value.PetId);

        _showItem = Item(ShowCatHeader, () => services.GetRequiredService<SettingsApplier>().ShowPet());
        _hideItem = Item(HideCatHeader, () => services.GetRequiredService<SettingsApplier>().HidePet());

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = AppConstants.ProductName, IsEnabled = false, FontWeight = FontWeights.SemiBold });
        menu.Items.Add(new Separator());
        menu.Items.Add(_showItem);
        menu.Items.Add(_hideItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(OpenSettingsHeader, OpenSettings));
        menu.Items.Add(Item(RestartHeader, () => services.GetRequiredService<AppLifecycle>().Restart()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(AboutHeader, OpenAbout));
        menu.Items.Add(Item(CheckForUpdatesHeader, () => _ = CheckForUpdatesAsync()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(ExitHeader, () => services.GetRequiredService<AppLifecycle>().Exit()));
        menu.Opened += (_, _) => RefreshMenuState();
        _menu = menu;

        _icon = new TaskbarIcon
        {
            ToolTipText = AppConstants.ProductName,
            IconSource = skin.GetClip(Domain.Pets.PetActivity.Idle).Frames[0],
            ContextMenu = menu
        };

        settings.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshMenuState);
        RefreshMenuState();
        logger.LogInformation("Tray icon shown");
    }

    /// <summary>Updates Show/Hide enablement and the tooltip from the live settings and registry state.</summary>
    public void RefreshMenuState()
    {
        var petHidden = !settings.Current.Pet.ShowPet;
        if (_showItem is not null)
        {
            _showItem.IsEnabled = petHidden;
        }

        if (_hideItem is not null)
        {
            _hideItem.IsEnabled = !petHidden;
        }

        if (_icon is not null)
        {
            _icon.ToolTipText = BuildTooltip(petHidden, startup.GetStatus());
        }
    }

    /// <summary>"WorkBuddy", a status line, and the version. Windows limits tooltips to 127 characters.</summary>
    public static string BuildTooltip(bool petHidden, StartupStatus startupStatus)
    {
        var lines = new List<string>
        {
            AppConstants.ProductName,
            petHidden ? "Pet Hidden" : "Running",
            $"Version {AppInfo.CurrentVersion}"
        };

        if (startupStatus.WillStartAtSignIn)
        {
            lines.Add("Starts with Windows");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = services.GetRequiredService<SettingsWindow>();
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void OpenAbout()
    {
        if (_aboutWindow is { IsLoaded: true })
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = services.GetRequiredService<AboutWindow>();
        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    /// <summary>Manual update check. Shows the result in a message box; never downloads or installs anything.</summary>
    public async Task CheckForUpdatesAsync()
    {
        if (_checkingForUpdates)
        {
            return;
        }

        _checkingForUpdates = true;
        try
        {
            var result = await services.GetRequiredService<IUpdateChecker>().CheckAsync(AppInfo.CurrentVersion);
            ShowUpdateResult(result);
        }
        finally
        {
            _checkingForUpdates = false;
        }
    }

    public static string DescribeUpdate(UpdateCheckResult result) => result.Outcome switch
    {
        UpdateOutcome.UpdateAvailable =>
            $"A new version of WorkBuddy is available.{Environment.NewLine}{Environment.NewLine}Current version: {result.Current}{Environment.NewLine}Latest version:  {result.Latest}{Environment.NewLine}{Environment.NewLine}Open the release page?",
        UpdateOutcome.UpToDate =>
            $"You're up to date.{Environment.NewLine}{Environment.NewLine}Current version: {result.Current}{Environment.NewLine}Latest version:  {result.Latest}",
        UpdateOutcome.NoReleases =>
            $"No releases have been published yet.{Environment.NewLine}{Environment.NewLine}Current version: {result.Current}",
        _ => $"Could not check for updates.{Environment.NewLine}{Environment.NewLine}{result.Error}"
    };

    private void ShowUpdateResult(UpdateCheckResult result)
    {
        var text = DescribeUpdate(result);
        if (result.Outcome == UpdateOutcome.UpdateAvailable)
        {
            var answer = MessageBox.Show(text, "WorkBuddy updates", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer == MessageBoxResult.Yes && result.ReleaseUrl is not null)
            {
                Process.Start(new ProcessStartInfo(result.ReleaseUrl) { UseShellExecute = true });
            }
        }
        else
        {
            MessageBox.Show(text, "WorkBuddy updates", MessageBoxButton.OK,
                result.Outcome == UpdateOutcome.Failed ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon?.Dispose();
        _icon = null;
        logger.LogInformation("Tray icon removed");
    }
}
