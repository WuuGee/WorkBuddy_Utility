using System.Reflection;

namespace FocusCatAI.Shared;

/// <summary>Product facts shown in the About dialog and used by the update check.</summary>
public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/WuuGee/WorkBuddy_Utility";
    public const string ReleasesPageUrl = RepositoryUrl + "/releases";
    public const string IssuesUrl = RepositoryUrl + "/issues";
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/WuuGee/WorkBuddy_Utility/releases/latest";

    public const string Copyright = "Copyright \u00a9 2026 Qian Tong Low";

    public const string Publisher = "Qian Tong Low";

    public const string LicenseSummary = "MIT License";

    public const string OpenSourceNotice =
        "WorkBuddy is built with open-source components, including Serilog, CommunityToolkit.Mvvm, " +
        "Microsoft.Extensions and Hardcodet.NotifyIcon.Wpf, each used under its own license.";

    /// <summary>The running version, normalised to major.minor.patch.</summary>
    public static Version CurrentVersion
    {
        get
        {
            var v = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }
}
