using System.Text.Json;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pets;

namespace FocusCatAI.Infrastructure;

/// <summary>The built-in pets, in picker order, followed by any valid community packs from the custom pets folder.</summary>
public sealed class StaticPetCatalog : IPetCatalog
{
    private static readonly PetDefinition[] BuiltIn =
    [
        new(PetDefinition.DefaultId, "Cream Cat"),
        new("tuxedo", "Tuxedo Cat", 2),
        new("ginger", "Ginger Cat", 3)
    ];

    public StaticPetCatalog() : this(null)
    {
    }

    public StaticPetCatalog(IAppPaths? paths)
    {
        var all = new List<PetDefinition>(BuiltIn);
        if (paths is not null)
        {
            all.AddRange(ScanCustomPets(paths.CustomPetsDirectory, BuiltIn.Select(p => p.Id)));
        }

        All = all;
    }

    public IReadOnlyList<PetDefinition> All { get; }

    public PetDefinition Default => All[0];

    public PetDefinition Resolve(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? Default
            : All.FirstOrDefault(p => string.Equals(p.Id, id.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;

    /// <summary>Finds valid community pet packs. Invalid folders are skipped silently; never throws.</summary>
    public static IReadOnlyList<PetDefinition> ScanCustomPets(string? directory, IEnumerable<string> reservedIds)
    {
        var result = new List<PetDefinition>();
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return result;
            }

            var taken = new HashSet<string>(reservedIds, StringComparer.OrdinalIgnoreCase);
            foreach (var dir in Directory.EnumerateDirectories(directory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var id = Path.GetFileName(dir);
                    if (string.IsNullOrWhiteSpace(id) || taken.Contains(id))
                    {
                        continue;
                    }

                    var display = TryReadDisplayName(dir, id);
                    if (display is null)
                    {
                        continue;
                    }

                    taken.Add(id);
                    result.Add(new PetDefinition(id, display, 1));
                }
                catch (Exception)
                {
                    // Skip bad pack.
                }
            }
        }
        catch (Exception)
        {
            // Unreadable folder: no custom pets.
        }

        return result;
    }

    private static string? TryReadDisplayName(string dir, string id)
    {
        var manifest = Path.Combine(dir, "pet.json");
        if (!File.Exists(manifest))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(manifest), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? display = null;
        JsonElement animations = default;
        var hasAnimations = false;
        foreach (var p in root.EnumerateObject())
        {
            if (p.Name.Equals("displayName", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
            {
                display = p.Value.GetString();
            }
            else if (p.Name.Equals("animations", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Object)
            {
                animations = p.Value;
                hasAnimations = true;
            }
        }

        if (!hasAnimations)
        {
            return null;
        }

        foreach (var a in animations.EnumerateObject())
        {
            if (!a.Name.Equals("Idle", StringComparison.OrdinalIgnoreCase) || a.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var f in a.Value.EnumerateObject())
            {
                if (f.Name.Equals("sheet", StringComparison.OrdinalIgnoreCase) && f.Value.ValueKind == JsonValueKind.String
                    && f.Value.GetString() is { Length: > 0 } sheet
                    && sheet.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                    && File.Exists(Path.Combine(dir, sheet)))
                {
                    return string.IsNullOrWhiteSpace(display) ? id : display.Trim();
                }
            }
        }

        return null;
    }
}
