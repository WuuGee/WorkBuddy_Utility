using FocusCatAI.Domain.Taskbar;

namespace FocusCatAI.Application.Taskbar;

/// <summary>Pure geometry helpers: free zones, spots beside icons, favorite resolution.</summary>
public static class TaskbarGeometry
{
    /// <summary>Gaps between taskbar elements that are at least <paramref name="minWidth"/> wide.</summary>
    public static IReadOnlyList<Zone> GetZones(TaskbarLayout layout, double minWidth)
    {
        var occupied = layout.Elements
            .Where(e => e.Right > layout.Left && e.Left < layout.Right)
            .Select(e => (L: Math.Max(e.Left, layout.Left), R: Math.Min(e.Right, layout.Right)))
            .OrderBy(e => e.L);

        var zones = new List<Zone>();
        var cursor = layout.Left;
        foreach (var (l, r) in occupied)
        {
            if (l - cursor >= minWidth)
            {
                zones.Add(new Zone(cursor, l));
            }

            cursor = Math.Max(cursor, r);
        }

        if (layout.Right - cursor >= minWidth)
        {
            zones.Add(new Zone(cursor, layout.Right));
        }

        return zones;
    }

    /// <summary>Left coordinate for a pet of <paramref name="width"/> standing in a zone as close to x as possible.</summary>
    public static double ClampToNearestZone(IReadOnlyList<Zone> zones, double x, double width)
    {
        var best = x;
        var bestDistance = double.MaxValue;
        foreach (var zone in zones)
        {
            var candidate = Math.Clamp(x, zone.Left, Math.Max(zone.Left, zone.Right - width));
            var distance = Math.Abs(candidate - x);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Position right next to an element (either side, whichever free zone is closest).</summary>
    public static double? SpotBeside(IReadOnlyList<Zone> zones, TaskbarElement element, double width)
    {
        const double gap = 2;
        var center = (element.Left + element.Right) / 2;
        double? best = null;
        var bestDistance = double.MaxValue;

        foreach (var zone in zones)
        {
            var max = Math.Max(zone.Left, zone.Right - width);
            foreach (var candidate in new[] { Math.Clamp(element.Left - width - gap, zone.Left, max), Math.Clamp(element.Right + gap, zone.Left, max) })
            {
                var distance = Math.Abs(candidate + width / 2 - center);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    public static TaskbarElement? FindElement(TaskbarLayout layout, string key) => key switch
    {
        "Start" => layout.Elements.FirstOrDefault(e => e.Kind == TaskbarElementKind.Start),
        "Search" => layout.Elements.FirstOrDefault(e => e.Kind == TaskbarElementKind.Search),
        "Clock" => layout.Elements.FirstOrDefault(e => e.Kind == TaskbarElementKind.Clock)
                   ?? layout.Elements.FirstOrDefault(e => e.Kind == TaskbarElementKind.Tray),
        _ when key.StartsWith("App:", StringComparison.OrdinalIgnoreCase) =>
            layout.Elements.FirstOrDefault(e => e.Kind == TaskbarElementKind.App && MatchesApp(e.Name, key[4..])),
        _ => null
    };

    // "Visual Studio" is a substring of "Visual Studio Code"; without this the favorite lands on VS Code's icon.
    private static bool MatchesApp(string name, string part) =>
        name.Contains(part, StringComparison.OrdinalIgnoreCase) &&
        !(part.Equals("Visual Studio", StringComparison.OrdinalIgnoreCase) &&
          name.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase));

    private const double AdjacentReach = 8;
    private const double LeftZoneSpan = 320;

    /// <summary>
    /// Where the pet stands for a favorite, and why. When the icon has free space right next to it the pet stands
    /// there (<c>Adjacent</c>). On a packed taskbar (e.g. Windows 11, left aligned) the icon is enclosed by other
    /// buttons, so the pet uses a semantic zone instead: <c>Start</c> (left edge), <c>Left</c> (spread by the icon's
    /// order in the app group), or <c>Clock</c> (right edge). That keeps different favorites at different places.
    /// </summary>
    public sealed record FavoriteResolution(string Key, double Desired, double Destination, string Zone, string? Reason);

    /// <summary>Resolves a favorite key to a destination, or null when the element is not on the taskbar or there is no free space.</summary>
    public static FavoriteResolution? ResolveFavoriteDetailed(TaskbarLayout layout, IReadOnlyList<Zone> zones, string key, double width)
    {
        var element = FindElement(layout, key);
        return element is null ? null : ResolveNear(layout, zones, element, key, width);
    }

    /// <summary>Same as <see cref="ResolveFavoriteDetailed"/> for an element that is already known.</summary>
    public static FavoriteResolution? ResolveNear(TaskbarLayout layout, IReadOnlyList<Zone> zones, TaskbarElement element, string key, double width)
    {
        if (zones.Count == 0)
        {
            return null;
        }

        var desired = (element.Left + element.Right) / 2 - width / 2;
        if (SpotBeside(zones, element, width) is { } beside)
        {
            var gap = beside + width <= element.Left ? element.Left - (beside + width) : beside - element.Right;
            if (gap <= AdjacentReach)
            {
                return new FavoriteResolution(key, desired, beside, "Adjacent", null);
            }
        }

        var nearest = zones.MinBy(z => Math.Max(0, Math.Max(z.Left - element.Right, element.Left - z.Right)))!;
        var distance = Math.Max(0, Math.Max(nearest.Left - element.Right, element.Left - nearest.Right));
        var reason = $"'{element.Name}' is enclosed by other buttons; nearest free space is {distance:F0}px away";

        switch (element.Kind)
        {
            case TaskbarElementKind.Start or TaskbarElementKind.Search or TaskbarElementKind.TaskView:
                return new FavoriteResolution(key, desired, zones[0].Left, "Start", reason);
            case TaskbarElementKind.Tray or TaskbarElementKind.Clock or TaskbarElementKind.Widgets:
                return new FavoriteResolution(key, desired, Math.Max(zones[^1].Left, zones[^1].Right - width), "Clock", reason);
        }

        var apps = layout.Elements.Where(e => e.Kind == TaskbarElementKind.App).OrderBy(e => e.Left).ToList();
        var index = Math.Max(0, apps.FindIndex(e => e.Left == element.Left));
        var fraction = apps.Count > 1 ? index / (double)(apps.Count - 1) : 0;
        var span = Math.Max(0, Math.Min(nearest.Width - width, LeftZoneSpan));
        return new FavoriteResolution(key, desired, nearest.Left + fraction * span, "Left", reason);
    }

    public static double? ResolveFavorite(TaskbarLayout layout, IReadOnlyList<Zone> zones, string key, double width)
    {
        var element = FindElement(layout, key);
        return element is null ? null : SpotBeside(zones, element, width);
    }
}
