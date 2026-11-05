using System.Text.Json;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Progression;
using FocusCatAI.Domain.Progression;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Infrastructure;

/// <summary>progression.json in the WorkBuddy data folder. Atomic write; a corrupt file is set aside and a fresh state is used.</summary>
public sealed class JsonProgressionStore : IProgressionStore
{
    public const string FileName = "progression.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly string _path;
    private readonly ILogger<JsonProgressionStore> _logger;

    public JsonProgressionStore(IAppPaths paths, ILogger<JsonProgressionStore> logger)
    {
        _path = Path.Combine(paths.RootDirectory, FileName);
        _logger = logger;
    }

    public ProgressionState Load()
    {
        if (!File.Exists(_path))
        {
            return new ProgressionState();
        }

        try
        {
            var state = JsonSerializer.Deserialize<ProgressionState>(File.ReadAllText(_path), JsonOptions) ?? new ProgressionState();
            return Sanitize(state);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            var aside = $"{_path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            _logger.LogError(ex, "Could not read progression.json; moving it to {Aside} and starting fresh", aside);
            try
            {
                File.Move(_path, aside);
            }
            catch (IOException moveError)
            {
                _logger.LogWarning(moveError, "Could not set the unreadable progression file aside");
            }

            return new ProgressionState();
        }
    }

    public void Save(ProgressionState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state with { SchemaVersion = ProgressionState.CurrentSchemaVersion }, JsonOptions));
            if (File.Exists(_path))
            {
                File.Replace(temp, _path, null);
            }
            else
            {
                File.Move(temp, _path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not save progression.json");
        }
    }

    private static ProgressionState Sanitize(ProgressionState s) => s with
    {
        TotalXp = Math.Max(0, s.TotalXp),
        TotalCompanionshipMinutes = Math.Max(0, s.TotalCompanionshipMinutes),
        DailyInteractionXp = Math.Clamp(s.DailyInteractionXp, 0, XpGrantPolicy.DailyInteractionCap),
        UnlockedAchievementIds = (s.UnlockedAchievementIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList()
    };
}
