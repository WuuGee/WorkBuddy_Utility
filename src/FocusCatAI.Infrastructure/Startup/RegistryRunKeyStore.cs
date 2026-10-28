using System.Runtime.Versioning;
using Microsoft.Win32;

namespace FocusCatAI.Infrastructure.Startup;

/// <summary>HKCU Run key access. Key paths are injectable so tests and verification can use a scratch location.</summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryRunKeyStore(
    string runKeyPath = RegistryRunKeyStore.DefaultRunKeyPath,
    string approvedKeyPath = RegistryRunKeyStore.DefaultApprovedKeyPath) : IRunKeyStore
{
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public string? GetCommand(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath);
        return key?.GetValue(valueName) as string;
    }

    public void SetCommand(string valueName, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true);
        key.SetValue(valueName, command, RegistryValueKind.String);
    }

    public void RemoveCommand(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    public bool IsDisabledByWindows(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(approvedKeyPath);

        // Windows stores 12 bytes; an even first byte (02/06) means enabled, an odd one (03/07) means disabled.
        return key?.GetValue(valueName) is byte[] { Length: > 0 } data && (data[0] & 1) == 1;
    }

    public void ClearDisabledByWindows(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(approvedKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    /// <summary>Reproduces what Task Manager does when the user switches a startup app off.</summary>
    public void SimulateDisabledByWindows(string valueName)
    {
        using var key = Registry.CurrentUser.CreateSubKey(approvedKeyPath, writable: true);
        var data = new byte[12];
        data[0] = 3;
        key.SetValue(valueName, data, RegistryValueKind.Binary);
    }
}
