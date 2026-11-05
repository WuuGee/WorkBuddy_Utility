using System.IO;
using FocusCatAI.Application.Abstractions;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Diagnostic runs (--verify-settings, --verify-startup, --verify-tray) keep progression in a scratch folder so they start
/// from a known fresh state and can never read or change the user's real progression.json.
/// </summary>
public static class VerifierScratch
{
    /// <summary>Set for diagnostic runs only; null in normal use.</summary>
    public static string? ProgressionDirectory { get; private set; }

    public static string Prepare(bool fresh)
    {
        var dir = Path.Combine(Path.GetTempPath(), "WorkBuddy-verify-progression");
        if (fresh && Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }

        Directory.CreateDirectory(dir);
        ProgressionDirectory = dir;
        return dir;
    }

    /// <summary>Paths whose root is the scratch folder; only the progression store uses it.</summary>
    public sealed class ScratchPaths(string root) : IAppPaths
    {
        public string RootDirectory => root;
        public string LogsDirectory => Path.Combine(root, "logs");
        public string ModelsDirectory => Path.Combine(root, "models");
    }
}
