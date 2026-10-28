namespace FocusCatAI.Application.Updates;

public enum UpdateOutcome
{
    UpToDate,
    UpdateAvailable,

    /// <summary>The repository has no published release yet.</summary>
    NoReleases,

    /// <summary>Offline, rate-limited, or an unreadable answer. Never an error dialog worth a crash.</summary>
    Failed
}

public sealed record UpdateCheckResult(UpdateOutcome Outcome, Version Current, Version? Latest, string? ReleaseUrl, string? Error);

/// <summary>Manual, on-demand check for a newer release. Never downloads or installs anything.</summary>
public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckAsync(Version current, CancellationToken cancellationToken = default);
}

public static class ReleaseVersion
{
    /// <summary>Normalises to major.minor.patch so 0.1.0 and 0.1.0.0 compare equal.</summary>
    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>Parses tags such as "v0.2.0", "0.2.0" or "v1.0.0-beta.1" (suffix ignored). Returns null when unreadable.</summary>
    public static Version? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var text = tag.Trim().TrimStart('v', 'V');
        var cut = text.IndexOfAny(['-', '+']);
        if (cut >= 0)
        {
            text = text[..cut];
        }

        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }
}
