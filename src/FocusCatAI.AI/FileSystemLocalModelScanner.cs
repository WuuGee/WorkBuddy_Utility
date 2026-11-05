using FocusCatAI.Application.Abstractions;

namespace FocusCatAI.AI;

/// <summary>Lists ONNX models under the models folder. Never throws.</summary>
public sealed class FileSystemLocalModelScanner(IAppPaths paths) : ILocalModelScanner
{
    public IReadOnlyList<string> ListInstalledModels()
    {
        var result = new List<string>();
        try
        {
            var root = paths.ModelsDirectory;
            Directory.CreateDirectory(root);

            foreach (var file in Directory.EnumerateFiles(root, "*.onnx", SearchOption.TopDirectoryOnly))
            {
                result.Add(Path.GetFileNameWithoutExtension(file));
            }

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                if (Directory.EnumerateFiles(dir, "*.onnx", SearchOption.AllDirectories).Any()
                    || File.Exists(Path.Combine(dir, "model.json")))
                {
                    result.Add(Path.GetFileName(dir));
                }
            }
        }
        catch (Exception)
        {
            // Unreadable or invalid path: report what was found so far.
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public bool HasAnyModel() => ListInstalledModels().Count > 0;
}
