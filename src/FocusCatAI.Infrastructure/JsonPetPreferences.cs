using System.Text.Json;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pets;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Infrastructure;

/// <summary>Persists the pet's favorite spots to pet-preferences.json (replaceable with a database later).</summary>
public sealed class JsonPetPreferences : IPetPreferences
{
    private const double MaxWeight = 5;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private sealed class FileModel
    {
        public int SchemaVersion { get; set; } = 1;
        public List<FavoriteSpot> Favorites { get; set; } = [];
        public bool Introduced { get; set; }
    }

    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<JsonPetPreferences> _logger;
    private readonly List<FavoriteSpot> _favorites;
    private bool _introduced;

    public JsonPetPreferences(IAppPaths paths, ILogger<JsonPetPreferences> logger)
    {
        _logger = logger;
        _path = Path.Combine(paths.RootDirectory, "pet-preferences.json");
        _favorites = Load();
    }

    public bool HasIntroduced
    {
        get
        {
            lock (_gate)
            {
                return _introduced;
            }
        }
    }

    public void MarkIntroduced()
    {
        lock (_gate)
        {
            _introduced = true;
        }
    }

    public void ResetFavorites()
    {
        lock (_gate)
        {
            _favorites.Clear();
            _favorites.AddRange(DefaultFavorites());
        }

        Save();
    }

    private static FavoriteSpot[] DefaultFavorites() =>
    [
        new("Start", 2),
        new("Clock", 1),
        new("App:Visual Studio", 2),
        new("App:Teams", 1)
    ];

    public IReadOnlyList<FavoriteSpot> Favorites
    {
        get
        {
            lock (_gate)
            {
                return _favorites.ToArray();
            }
        }
    }

    public void RecordVisit(string key)
    {
        lock (_gate)
        {
            var index = _favorites.FindIndex(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _favorites[index] = _favorites[index] with { Weight = Math.Min(MaxWeight, _favorites[index].Weight + 0.1) };
            }
        }
    }

    public void Save()
    {
        try
        {
            FileModel model;
            lock (_gate)
            {
                model = new FileModel { Favorites = _favorites.ToList(), Introduced = _introduced };
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(model, JsonOptions));
            File.Move(temp, _path, overwrite: true);
            _logger.LogInformation("Saved pet preferences");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save pet preferences");
        }
    }

    private List<FavoriteSpot> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var model = JsonSerializer.Deserialize<FileModel>(File.ReadAllText(_path), JsonOptions);
                _introduced = model?.Introduced ?? false;
                if (model?.Favorites is { Count: > 0 })
                {
                    return model.Favorites;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read pet preferences, using defaults");
        }

        return [.. DefaultFavorites()];
    }
}
