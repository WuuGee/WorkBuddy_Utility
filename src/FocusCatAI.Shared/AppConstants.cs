namespace FocusCatAI.Shared;

public static class AppConstants
{
    public const string ProductName = "WorkBuddy";
    public const string AppName = "WorkBuddy";
    public const string LegacyAppName = "FocusCatAI";
    public const string SingleInstanceMutexName = "WorkBuddy.SingleInstance";

    /// <summary>Name of the value under HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
    public const string StartupValueName = "WorkBuddy";

    /// <summary>Command-line switch added to the Run key command. Marks a launch by Windows sign-in.</summary>
    public const string AutostartArgument = "--autostart";
}
