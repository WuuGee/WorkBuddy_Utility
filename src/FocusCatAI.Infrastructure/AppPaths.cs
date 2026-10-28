using FocusCatAI.Application.Abstractions;
using FocusCatAI.Shared;

namespace FocusCatAI.Infrastructure;

public sealed class AppPaths : IAppPaths
{
    public string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppConstants.AppName);

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");
    public string ModelsDirectory => Path.Combine(RootDirectory, "models");
}
