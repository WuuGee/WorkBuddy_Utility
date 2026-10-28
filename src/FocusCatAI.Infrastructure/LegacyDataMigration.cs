using Microsoft.Extensions.Logging;

namespace FocusCatAI.Infrastructure;

/// <summary>
/// One-time copy of the pre-rename data folder (%LOCALAPPDATA%\FocusCatAI) into the WorkBuddy folder.
/// The legacy folder is left untouched so a failed or unwanted migration never loses data.
/// </summary>
public static class LegacyDataMigration
{
    private const string MarkerFile = ".migrated-from-focuscatai";

    // Logs are not migrated: they are diagnostics, not user data.
    private static readonly string[] ExcludedDirectories = ["logs"];

    /// <summary>Copies legacy user data once. Returns the number of files copied (0 when nothing was done).</summary>
    public static int Run(string legacyRoot, string newRoot, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRoot);

        var marker = Path.Combine(newRoot, MarkerFile);
        if (File.Exists(marker) || !Directory.Exists(legacyRoot))
        {
            return 0;
        }

        Directory.CreateDirectory(newRoot);
        var copied = 0;
        foreach (var source in Directory.EnumerateFiles(legacyRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(legacyRoot, source);
            if (ExcludedDirectories.Any(d => relative.StartsWith(d + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var target = Path.Combine(newRoot, relative);
            if (File.Exists(target))
            {
                continue; // never overwrite data the new install already has
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
            copied++;
        }

        File.WriteAllText(marker, $"Migrated from {legacyRoot} on {DateTime.UtcNow:O}");
        logger?.LogInformation("Migrated {Count} file(s) from {Legacy} to {New}", copied, legacyRoot, newRoot);
        return copied;
    }
}
