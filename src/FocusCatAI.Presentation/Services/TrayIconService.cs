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
    IPetCatalog pets,
    IOptions<PetOptions> options,
    ISettingsService settings,
    IStartupManager startup,
    IServiceProvider services,
    IExternalLauncher launcher,
    ILogger<TrayIconService> logger) : IDisposable
{
    public const string ShowCatHeader = "Show Cat";
    public const string HideCatHeader = "Hide Cat";
    public const string OpenSettingsHeader = "Open Settings";
    public const string PetHeader = "Pet";
    public const string RestartHeader = "Restart WorkBuddy";
    public const string AboutHeader = "About";
    public const string CheckForUpdatesHeader = "Check for Updates";
    public const string OpenLogsHeader = "Open Logs Folder";
    public const string ReportIssueHeader = "Report an Issue";
    public const string ExitHeader = "Exit";

    private TaskbarIcon? _icon;
    private ContextMenu? _menu;
    private MenuItem? _showItem;
    private MenuItem? _hideItem;
    private MenuItem? _petMenu;
    private string? _iconPetId;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private bool _checkingForUpdates;

    // Set once the update balloon has been shown. The update balloon is the only balloon this app shows, so a click
    // can only mean "open the release page". Deliberately not cleared on TrayBalloonTipClosed: Windows 11 can raise
    // Closed before Clicked, or not at all, which would race with the click.
    private string? _pendingUpdateReleaseUrl;

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
        var skin = sprites.Load(pets.Resolve(settings.Current.Pet.SelectedPetId).Id);
        _iconPetId = pets.Resolve(settings.Current.Pet.SelectedPetId).Id;

        _petMenu = new MenuItem { Header = PetHeader };
        foreach (var pet in pets.All)
        {
            var petId = pet.Id;
            var petItem = new MenuItem { Header = pet.DisplayName, Tag = petId, IsCheckable = false };
            petItem.Click += (_, _) => SelectPet(petId);
            _petMenu.Items.Add(petItem);
        }

        _showItem = Item(ShowCatHeader, () => services.GetRequiredService<SettingsApplier>().ShowPet());
        _hideItem = Item(HideCatHeader, () => services.GetRequiredService<SettingsApplier>().HidePet());

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = AppConstants.ProductName, IsEnabled = false, FontWeight = FontWeights.SemiBold });
        menu.Items.Add(new Separator());
        menu.Items.Add(_showItem);
        menu.Items.Add(_hideItem);
        menu.Items.Add(_petMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(OpenSettingsHeader, OpenSettings));
        menu.Items.Add(Item(RestartHeader, () => services.GetRequiredService<AppLifecycle>().Restart()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(AboutHeader, OpenAbout));
        menu.Items.Add(Item(CheckForUpdatesHeader, () => _ = CheckForUpdatesAsync()));
        menu.Items.Add(Item(OpenLogsHeader, launcher.OpenLogsFolder));
        menu.Items.Add(Item(ReportIssueHeader, () => launcher.OpenUrl(AppInfo.IssuesUrl)));
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

        _icon.TrayBalloonTipClicked += (_, _) => OnBalloonClicked();

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
            var selected = pets.Resolve(settings.Current.Pet.SelectedPetId).Id;
            if (!string.Equals(selected, _iconPetId, StringComparison.OrdinalIgnoreCase))
            {
                _iconPetId = selected;
                _icon.IconSource = sprites.Load(selected).GetClip(Domain.Pets.PetActivity.Idle).Frames[0];
            }
        }

        if (_petMenu is not null)
        {
            var current = pets.Resolve(settings.Current.Pet.SelectedPetId).Id;
            foreach (var item in _petMenu.Items.OfType<MenuItem>())
            {
                item.IsChecked = string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>Selects a pet through settings, the single source of truth; every view follows <c>Changed</c>.</summary>
    public void SelectPet(string petId)
    {
        settings.Update(s => s with { Pet = s.Pet with { SelectedPetId = petId } });
        RefreshMenuState();
    }

    /// <summary>The Pet submenu items, for diagnostics: (id, header, checked).</summary>
    public IReadOnlyList<(string Id, string Header, bool Checked)> PetMenuItems =>
        _petMenu?.Items.OfType<MenuItem>().Select(i => ((string)i.Tag!, (string)i.Header, i.IsChecked)).ToList() ?? [];

    /// <summary>Real submenu item for a pet id so diagnostics can click what a user would.</summary>
    public MenuItem? FindPetMenuItem(string petId) =>
        _petMenu?.Items.OfType<MenuItem>().FirstOrDefault(i => i.Tag as string == petId);

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

    /// <summary>
    /// One quiet check at startup. Shows a balloon only when a newer release exists; being up to date, offline or
    /// rate-limited produces no UI at all. Never throws and never blocks the caller.
    /// </summary>
    public async Task CheckForUpdatesQuietlyAsync()
    {
        try
        {
            var result = await services.GetRequiredService<IUpdateChecker>().CheckAsync(AppInfo.CurrentVersion).ConfigureAwait(false);
            if (result.Outcome != UpdateOutcome.UpdateAvailable || result.ReleaseUrl is null)
            {
                logger.LogDebug("Startup update check: {Outcome}", result.Outcome);
                return;
            }

            await (System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => ShowUpdateBalloon(result)).Task ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup update check failed");
        }
    }

    private void ShowUpdateBalloon(UpdateCheckResult result)
    {
        if (_icon is null)
        {
            return;
        }

        _pendingUpdateReleaseUrl = result.ReleaseUrl;
        _icon.ShowBalloonTip("WorkBuddy update available", $"Version {result.Latest} is available. Click to open the download page.", BalloonIcon.Info);
        logger.LogInformation("Update {Latest} available (current {Current}); balloon shown", result.Latest, result.Current);
    }

    private void OnBalloonClicked()
    {
        if (!string.IsNullOrEmpty(_pendingUpdateReleaseUrl))
        {
            launcher.OpenUrl(_pendingUpdateReleaseUrl);
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
                launcher.OpenUrl(result.ReleaseUrl);
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
