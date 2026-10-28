using System.Text.Json.Nodes;

namespace FocusCatAI.Infrastructure;

/// <summary>
/// Upgrades an older settings.json to the current layout before it is deserialized. Each step moves the document
/// from version N to N+1 (rename a key, move a section, ...). Missing keys need no step: every setting has a default.
/// </summary>
public static class SettingsMigrator
{
    private const string VersionKey = "SchemaVersion";

    /// <summary>Steps keyed by the version they upgrade FROM. Empty for now: version 1 is the first layout.</summary>
    public static IReadOnlyDictionary<int, Action<JsonObject>> Steps { get; } = new Dictionary<int, Action<JsonObject>>();

    /// <summary>Reads the stored version; a missing or invalid value counts as 1.</summary>
    public static int ReadVersion(JsonObject root) =>
        root[VersionKey] is JsonValue v && v.TryGetValue<int>(out var version) && version > 0 ? version : 1;

    /// <summary>Applies every step between the stored version and <paramref name="target"/>. Returns true when the document changed.</summary>
    public static bool Migrate(JsonObject root, int target, IReadOnlyDictionary<int, Action<JsonObject>>? steps = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        steps ??= Steps;

        var version = ReadVersion(root);
        if (version >= target)
        {
            return false;
        }

        for (; version < target; version++)
        {
            if (steps.TryGetValue(version, out var step))
            {
                step(root);
            }
        }

        root[VersionKey] = target;
        return true;
    }
}
