using System.Windows;
using System.Windows.Controls;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Pets;
using FocusCatAI.Application.Updates;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Presentation.Animation;
using FocusCatAI.Presentation.Configuration;
using FocusCatAI.Presentation.Views;
using FocusCatAI.Shared;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// The tray icon and its menu: application management only (pet visibility, settings, restart, about, updates, exit).
/// Pomodoro is deliberately not in the tray.
/// </summary>
public sealed class TrayIconService(
    ISpriteManager sprites,
    IPetCatalog pets,
    IAccessoryCatalog accessories,
    ISettingsService settings,
    IStartupManager startup,
    IServiceProvider services,
    IExternalLauncher launcher,
    FocusCatAI.Application.Progression.IProgressionService progression,
    ILogger<TrayIconService> logger) : IDisposable
{
    public const string CompanionPrefix = "Companion: ";
    public const string ShowCatHeader = "Show Cat";
    public const string HideCatHeader = "Hide Cat";
    public const string OpenSettingsHeader = "Open Settings";
    public const string PetHeader = "Pet";
    public const string AccessoryHeader = "Accessory";
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
    private MenuItem? _accessoryMenu;
    private MenuItem? _companionItem;
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
        _iconPetId = pets.Effective(settings.Current.Pet.SelectedPetId, progression.Progress.Level).Id;
        var skin = sprites.Load(_iconPetId);

        _petMenu = new MenuItem { Header = PetHeader };
        foreach (var pet in pets.All)
        {
            var petId = pet.Id;
            var petItem = new MenuItem { Header = PetUnlockPolicy.DisplayLabel(pet, progression.Progress.Level), Tag = petId, IsCheckable = false, IsEnabled = PetUnlockPolicy.IsUnlocked(pet, progression.Progress.Level) };
            petItem.Click += (_, _) => SelectPet(petId);
            _petMenu.Items.Add(petItem);
        }

        _accessoryMenu = new MenuItem { Header = AccessoryHeader };
        foreach (var accessory in accessories.All)
        {
            var accessoryId = accessory.Id;
            var accessoryItem = new MenuItem { Header = Domain.Accessories.AccessoryUnlockPolicy.DisplayLabel(accessory, progression.Progress.Level), Tag = accessoryId, IsCheckable = false, IsEnabled = Domain.Accessories.AccessoryUnlockPolicy.IsUnlocked(accessory, progression.Progress.Level) };
            accessoryItem.Click += (_, _) => SelectAccessory(accessoryId);
            _accessoryMenu.Items.Add(accessoryItem);
        }

        _showItem = Item(ShowCatHeader, () => services.GetRequiredService<SettingsApplier>().ShowPet());
        _hideItem = Item(HideCatHeader, () => services.GetRequiredService<SettingsApplier>().HidePet());

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = AppConstants.ProductName, IsEnabled = false, FontWeight = FontWeights.SemiBold });
        _companionItem = new MenuItem { IsEnabled = false };
        menu.Items.Add(_companionItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_showItem);
        menu.Items.Add(_hideItem);
        menu.Items.Add(_petMenu);
        menu.Items.Add(_accessoryMenu);
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
        progression.LeveledUp += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshMenuState);
        RefreshMenuState();
        logger.LogInformation("Tray icon shown");
    }

    /// <summary>Updates Show/Hide enablement and the tooltip from the live settings and registry state.</summary>
    public void RefreshMenuState()
    {
        var petHidden = !settings.Current.Pet.ShowPet;
        if (_companionItem is not null)
        {
            _companionItem.Header = DescribeCompanion(progression.Progress);
        }
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
            var selected = pets.Effective(settings.Current.Pet.SelectedPetId, progression.Progress.Level).Id;
            if (!string.Equals(selected, _iconPetId, StringComparison.OrdinalIgnoreCase))
            {
                _iconPetId = selected;
                _icon.IconSource = sprites.Load(selected).GetClip(Domain.Pets.PetActivity.Idle).Frames[0];
            }
        }

        if (_petMenu is not null)
        {
            var current = pets.Effective(settings.Current.Pet.SelectedPetId, progression.Progress.Level).Id;
            var level = progression.Progress.Level;
            foreach (var item in _petMenu.Items.OfType<MenuItem>())
            {
                var def = pets.Resolve(item.Tag as string);
                item.Header = PetUnlockPolicy.DisplayLabel(def, level);
                item.IsEnabled = PetUnlockPolicy.IsUnlocked(def, level);
                item.IsChecked = string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase);
            }
        }
        if (_accessoryMenu is not null)
        {
            var level = progression.Progress.Level;
            var current = Application.Accessories.AccessorySelection.Effective(accessories, settings.Current.Pet.SelectedAccessoryId, level).Id;
            foreach (var item in _accessoryMenu.Items.OfType<MenuItem>())
            {
                var def = accessories.Resolve(item.Tag as string);
                item.Header = Domain.Accessories.AccessoryUnlockPolicy.DisplayLabel(def, level);
                item.IsEnabled = Domain.Accessories.AccessoryUnlockPolicy.IsUnlocked(def, level);
                item.IsChecked = string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>The read-only status line, e.g. <c>Companion: Lv. 1 (0 / 50 XP)</c>.</summary>
    public static string DescribeCompanion(Domain.Progression.LevelProgress p) =>
        p.IsMax ? $"{CompanionPrefix}Lv. {p.Level} (MAX)" : $"{CompanionPrefix}Lv. {p.Level} ({p.CurrentLevelXp} / {p.NextLevelXp} XP)";

    /// <summary>Selects a pet through settings, the single source of truth; every view follows <c>Changed</c>.</summary>
    public void SelectPet(string petId)
    {
        settings.Update(s => s with { Pet = s.Pet with { SelectedPetId = petId } });
        RefreshMenuState();
    }

    /// <summary>The Pet submenu items, for diagnostics: (id, header, checked).</summary>
    public IReadOnlyList<(string Id, string Header, bool Checked)> PetMenuItems =>
        _petMenu?.Items.OfType<MenuItem>().Select(i => ((string)i.Tag!, (string)i.Header, i.IsChecked)).ToList() ?? [];

    /// <summary>Selects an accessory through settings, the single source of truth; every view follows <c>Changed</c>.</summary>
    public void SelectAccessory(string accessoryId)
    {
        settings.Update(s => s with { Pet = s.Pet with { SelectedAccessoryId = accessoryId } });
        RefreshMenuState();
    }

    /// <summary>The Accessory submenu items, for diagnostics: (id, header, checked, enabled).</summary>
    public IReadOnlyList<(string Id, string Header, bool Checked, bool Enabled)> AccessoryMenuItems =>
        _accessoryMenu?.Items.OfType<MenuItem>().Select(i => ((string)i.Tag!, (string)i.Header, i.IsChecked, i.IsEnabled)).ToList() ?? [];

    /// <summary>Real submenu item for an accessory id so diagnostics can click what a user would.</summary>
    public MenuItem? FindAccessoryMenuItem(string accessoryId) =>
        _accessoryMenu?.Items.OfType<MenuItem>().FirstOrDefault(i => i.Tag as string == accessoryId);

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
