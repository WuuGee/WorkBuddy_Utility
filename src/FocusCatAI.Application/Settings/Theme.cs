using System.Text.Json;
using System.Text.Json.Serialization;

namespace FocusCatAI.Application.Settings;

/// <summary>The user's choice. <see cref="System"/> follows the Windows app theme.</summary>
public enum ThemePreference
{
    System,
    Light,
    Dark
}

/// <summary>The theme actually shown once <see cref="ThemePreference.System"/> has been resolved.</summary>
public enum EffectiveTheme
{
    Light,
    Dark
}

/// <summary>Pure resolution of the preference; no OS access, so it is unit-testable.</summary>
public static class ThemeResolver
{
    public static EffectiveTheme Resolve(ThemePreference preference, bool systemPrefersLight) => preference switch
    {
        ThemePreference.Light => EffectiveTheme.Light,
        ThemePreference.Dark => EffectiveTheme.Dark,
        _ => systemPrefersLight ? EffectiveTheme.Light : EffectiveTheme.Dark
    };
}

/// <summary>
/// Reads a theme from settings.json. Anything unknown (a misspelt name, an out-of-range number, the wrong JSON type)
/// becomes <see cref="ThemePreference.System"/> instead of failing the whole file.
/// </summary>
public sealed class ThemePreferenceConverter : JsonConverter<ThemePreference>
{
    public override ThemePreference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String when Enum.TryParse<ThemePreference>(reader.GetString(), true, out var named) && Enum.IsDefined(named):
                return named;
            case JsonTokenType.Number when reader.TryGetInt32(out var number) && Enum.IsDefined(typeof(ThemePreference), number):
                return (ThemePreference)number;
            case JsonTokenType.StartObject or JsonTokenType.StartArray:
                reader.Skip();
                return ThemePreference.System;
            default:
                return ThemePreference.System;
        }
    }

    public override void Write(Utf8JsonWriter writer, ThemePreference value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
