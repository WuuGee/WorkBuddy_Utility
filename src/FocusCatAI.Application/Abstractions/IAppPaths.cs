namespace FocusCatAI.Application.Abstractions;

public interface IAppPaths
{
    string RootDirectory { get; }
    string LogsDirectory { get; }
    string ModelsDirectory { get; }
}
