using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Taskbar;

namespace FocusCatAI.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Driven by the pet's single UI timer, always on the UI thread.</summary>
public interface ITickable
{
    void Tick(TimeSpan elapsed);
}

public interface IUserIdleProvider
{
    /// <summary>Time since the last keyboard/mouse input. Never exposes what was typed.</summary>
    TimeSpan IdleTime { get; }
}

public sealed record ForegroundApp(string ProcessName, string? Title);

public interface IForegroundAppProvider
{
    /// <summary>Process name of the active window. Title is null unless website detection is enabled.</summary>
    ForegroundApp? GetForeground();
}

public sealed class MonitoringOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Opt-in: read the active window title transiently (e.g. to spot GitHub). Titles are never stored or logged.</summary>
    public bool DetectWebsiteTitles { get; set; }
}

/// <summary>Shared, UI-thread-owned snapshot of where the pet is and what surrounds it.</summary>
public interface IPetWorld
{
    double PetX { get; set; }
    double PetWidth { get; set; }
    double PetHeight { get; set; }

    /// <summary>Top edge of the pet window in DIPs (used to place the speech bubble).</summary>
    double PetTop { get; set; }

    /// <summary>Base walking speed in DIPs per second for the current pet size.</summary>
    double WalkSpeed { get; set; }

    TaskbarLayout Layout { get; set; }

    /// <summary>Mouse X in DIPs when the cursor is on the taskbar band, otherwise null.</summary>
    double? CursorX { get; set; }
}

public interface ITaskbarLayoutProvider
{
    TaskbarLayout Current { get; }
}

/// <summary>Describes which animations the active pet provides, so behavior stays data-driven.</summary>
public interface IAnimationCatalog
{
    bool Has(PetActivity activity);
    IReadOnlyList<PetActivity> WithTag(string tag);
}

public interface IPetPreferences
{
    IReadOnlyList<FavoriteSpot> Favorites { get; }
    void RecordVisit(string key);
    void Save();

    /// <summary>True once the one-time first-launch greeting has been delivered. Never reset.</summary>
    bool HasIntroduced { get; }

    void MarkIntroduced();

    /// <summary>Restores the default favorite spots and weights. The introduced flag is kept.</summary>
    void ResetFavorites();
}

public interface ISpeechPresenter
{
    void Show(string text, TimeSpan duration);
}
