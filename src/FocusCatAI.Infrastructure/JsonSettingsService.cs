using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Settings;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Infrastructure;

/// <summary>
/// settings.json in the WorkBuddy data folder. Human-readable, versioned, written atomically (temp file then replace,
/// with a .bak of the previous version). A corrupt file is set aside and defaults are used; a file from a newer
/// version is backed up once before it is overwritten.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    public const string FileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<JsonSettingsService> _logger;
    private AppSettings _current;

    public JsonSettingsService(IAppPaths paths, ILogger<JsonSettingsService> logger)
    {
        _logger = logger;
        _path = Path.Combine(paths.RootDirectory, FileName);
        _current = Load();
    }

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler<AppSettings>? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        AppSettings next;
        lock (_gate)
        {
            next = SettingsLimits.Sanitize(change(_current) with { SchemaVersion = AppSettings.CurrentSchemaVersion });
            if (next == _current)
            {
                return;
            }

            _current = next;
            Save(next);
        }

        Changed?.Invoke(this, next);
    }

    public void ResetToDefaults() => Update(_ => new AppSettings());

    private AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            var defaults = new AppSettings();
            Save(defaults);
            _logger.LogInformation("Created default settings at {Path}", _path);
            return defaults;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(_path)) as JsonObject
                       ?? throw new JsonException("settings.json is not a JSON object");

            var stored = SettingsMigrator.ReadVersion(root);
            if (stored > AppSettings.CurrentSchemaVersion)
            {
                var backup = $"{_path}.v{stored}.bak";
                if (!File.Exists(backup))
                {
                    File.Copy(_path, backup);
                }

                _logger.LogWarning("settings.json is from a newer version ({Stored}); backed up to {Backup}", stored, backup);
            }

            var migrated = SettingsMigrator.Migrate(root, AppSettings.CurrentSchemaVersion);
            var settings = SettingsLimits.Sanitize(root.Deserialize<AppSettings>(JsonOptions) ?? new AppSettings());
            if (migrated)
            {
                Save(settings);
                _logger.LogInformation("Migrated settings.json from version {From} to {To}", stored, AppSettings.CurrentSchemaVersion);
            }

            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var aside = $"{_path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            _logger.LogError(ex, "Could not read settings.json; moving it to {Aside} and using defaults", aside);
            try
            {
                File.Move(_path, aside);
            }
            catch (IOException moveError)
            {
                _logger.LogWarning(moveError, "Could not set the unreadable settings file aside");
            }

            return new AppSettings();
        }
    }

    private void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            if (File.Exists(_path))
            {
                File.Replace(temp, _path, _path + ".bak");
            }
            else
            {
                File.Move(temp, _path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings stay applied in memory for this session; the next change retries the write.
            _logger.LogError(ex, "Failed to save settings.json");
        }
    }
}
