using System.Text.Json;
using System.Text.RegularExpressions;
using FocusCatAI.Application.Abstractions;

namespace FocusCatAI.Infrastructure;

/// <summary>Reads custom-theme.json: a flat dictionary of "Brush.*" keys to #RRGGBB / #AARRGGBB strings. Never throws.</summary>
public static class CustomThemeLoader
{
    public const string FileName = "custom-theme.json";

    private static readonly Regex HexColor = new("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValidHex(string? value) => value is not null && HexColor.IsMatch(value);

    public static IReadOnlyDictionary<string, string> Load(IAppPaths paths)
    {
        try
        {
            var file = Path.Combine(paths.CustomThemesDirectory, FileName);
            return File.Exists(file) ? Parse(File.ReadAllText(file)) : new Dictionary<string, string>();
        }
        catch (Exception)
        {
            return new Dictionary<string, string>();
        }
    }

    public static IReadOnlyDictionary<string, string> Parse(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.StartsWith("Brush.", StringComparison.Ordinal)
                    && prop.Name.Length > "Brush.".Length
                    && prop.Value.ValueKind == JsonValueKind.String
                    && IsValidHex(prop.Value.GetString()))
                {
                    result[prop.Name] = prop.Value.GetString()!;
                }
            }
        }
        catch (Exception)
        {
            // Corrupt JSON: ignore.
        }

        return result;
    }
}
