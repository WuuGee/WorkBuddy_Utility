using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Taskbar;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Taskbar;
using Microsoft.Extensions.Logging;
using static FocusCatAI.Domain.Pets.PetBehaviorType;

namespace FocusCatAI.Application.Pets;

/// <summary>Chooses what the pet does next from its mood, the taskbar layout and learned favorite spots.</summary>
public sealed class BehaviorPlanner(
    IRandomSource random,
    IMoodService mood,
    IPetWorld world,
    IAnimationCatalog catalog,
    IPetPreferences preferences,
    ILogger<BehaviorPlanner>? logger = null) : IBehaviorPlanner
{
    private static (PetBehaviorType Type, double Weight)[] W(double roam, double sit, double sleep, double observe, double play, double follow) =>
    [
        (Roaming, roam), (Sitting, sit), (Sleeping, sleep), (Observing, observe), (Playing, play), (Following, follow)
    ];

    private static readonly Dictionary<PetMood, (PetBehaviorType Type, double Weight)[]> Weights = new()
    {
        [PetMood.Relaxed] = W(25, 25, 5, 15, 5, 5),
        [PetMood.Curious] = W(30, 10, 0, 30, 10, 15),
        [PetMood.Happy] = W(25, 5, 0, 10, 35, 10),
        [PetMood.Sleepy] = W(5, 20, 60, 5, 0, 0),
        [PetMood.Focused] = W(3, 60, 0, 30, 0, 0)
    };

    private PetBehaviorType? _last;
    private PetActivity _lastFidget;

    public IdleFidget? NextFidget()
    {
        var calm = mood.Current == PetMood.Focused;
        var options = new (PetActivity Activity, double Weight, double Seconds)[]
        {
            (PetActivity.Blink, 30, 0.6),
            (PetActivity.TailFlick, 20, 0.9),
            (PetActivity.LookLeft, 14, 1.3),
            (PetActivity.LookRight, 14, 1.3),
            (PetActivity.Groom, calm ? 3 : 12, 3.2),
            (PetActivity.Stretch, calm ? 2 : 6, 1.7),
            (PetActivity.Sit, 15, 3.0),
            (PetActivity.Idle, 15, 3.0)
        }.Where(o => catalog.Has(o.Activity) && o.Activity != _lastFidget).ToList();

        if (options.Count == 0)
        {
            return null;
        }

        var roll = random.NextDouble() * options.Sum(o => o.Weight);
        var chosen = options[^1];
        foreach (var option in options)
        {
            if (roll < option.Weight)
            {
                chosen = option;
                break;
            }

            roll -= option.Weight;
        }

        _lastFidget = chosen.Activity;
        return new IdleFidget(chosen.Activity, chosen.Seconds, Range(2.5, 7) * DwellScale);
    }

    public BehaviorPlan Plan()
    {
        var width = world.PetWidth;
        var zones = TaskbarGeometry.GetZones(world.Layout, width);
        var type = Choose(zones);
        _last = type;

        var plan = type switch
        {
            Roaming => PlanRoaming(zones),
            Sleeping => PlanSleeping(zones),
            Observing => PlanObserving(zones),
            Playing => PlanPlaying(zones),
            Following => PlanFollowing(zones),
            _ => null
        };

        return Log(plan ?? PlanSitting(zones));
    }

    /// <summary>Info-level trace of what the pet decided, so companion behavior (favorites, targets) can be validated from the log.</summary>
    private BehaviorPlan Log(BehaviorPlan plan)
    {
        var weight = plan.FavoriteKey is null
            ? (double?)null
            : preferences.Favorites.FirstOrDefault(f => f.Key == plan.FavoriteKey)?.Weight;
        logger?.LogInformation(
            "[Behavior] Type={Type} Target={Target} Weight={Weight} DestinationX={X} Activity={Activity} Seconds={Seconds:F0}",
            plan.Type, plan.FavoriteKey ?? "(none)", weight is { } w ? w.ToString("F2") : "-",
            plan.TargetX is { } x ? x.ToString("F0") : "(stay)", plan.Activity, plan.Seconds);
        return plan;
    }

    private PetBehaviorType Choose(IReadOnlyList<Zone> zones)
    {
        var options = Weights[mood.Current]
            .Select(w => (w.Type, Weight: Adjust(w.Type, w.Weight, zones)))
            .Where(o => o.Weight > 0)
            .ToList();

        var roll = random.NextDouble() * options.Sum(o => o.Weight);
        foreach (var option in options)
        {
            if (roll < option.Weight)
            {
                return option.Type;
            }

            roll -= option.Weight;
        }

        return Sitting;
    }

    private double Adjust(PetBehaviorType type, double weight, IReadOnlyList<Zone> zones)
    {
        var needsZones = type is Roaming or Observing or Playing or Following;
        if (needsZones && zones.Count == 0)
        {
            return 0;
        }

        if (type == Following && world.CursorX is null)
        {
            return 0;
        }

        return type == _last ? weight * 0.25 : weight;
    }

    private double Range(double min, double max) => min + random.NextDouble() * (max - min);

    private double DwellScale => mood.Current switch
    {
        PetMood.Focused => 2.0,
        PetMood.Sleepy => 1.5,
        PetMood.Happy => 0.8,
        _ => 1.0
    };

    private PetActivity Variant(string tag, PetActivity fallback)
    {
        var list = catalog.WithTag(tag);
        if (list.Count > 0)
        {
            return list[random.Next(0, list.Count)];
        }

        return catalog.Has(fallback) ? fallback : PetActivity.Idle;
    }

    private BehaviorPlan PlanSitting(IReadOnlyList<Zone> zones)
    {
        // The cat likes to live at its favorite spots (Start, Visual Studio, ...): often walk there before sitting.
        if (zones.Count > 0 && random.NextDouble() < 0.4 &&
            PickFavorite(zones, out var key) is { } spot && Math.Abs(spot - world.PetX) > 4)
        {
            return new BehaviorPlan(Sitting, PetState.Idle, Variant("sit", PetActivity.Sit), Range(10, 25) * DwellScale,
                spot, 1.0, null, key);
        }

        return new(Sitting, PetState.Idle, Variant(random.NextDouble() < 0.15 ? "stretch" : "sit", PetActivity.Sit), Range(6, 15) * DwellScale);
    }

    private BehaviorPlan? PlanRoaming(IReadOnlyList<Zone> zones)
    {
        var target = PickRoamTarget(zones, out var key);
        if (target is null)
        {
            return null;
        }

        var activity = key is null ? Variant("idle", PetActivity.Idle) : Variant("sit", PetActivity.Sit);
        return new BehaviorPlan(Roaming, PetState.Idle, activity, Range(3, 8) * DwellScale, target, 1.0, target >= world.PetX, key);
    }

    private double? PickRoamTarget(IReadOnlyList<Zone> zones, out string? key)
    {
        key = null;
        var roll = random.NextDouble();
        if (roll < 0.5 && PickFavorite(zones, out key) is { } favorite)
        {
            return favorite;
        }

        key = null;
        var zone = PickZone(zones);
        if (roll < 0.6)
        {
            return random.NextDouble() < 0.5 ? zone.Left : zone.Right - world.PetWidth;
        }

        return zone.Left + random.NextDouble() * Math.Max(0, zone.Width - world.PetWidth);
    }

    private double? PickFavorite(IReadOnlyList<Zone> zones, out string? key)
    {
        var resolved = new List<(string Key, double X, double Weight)>();
        foreach (var favorite in preferences.Favorites)
        {
            var resolution = TaskbarGeometry.ResolveFavoriteDetailed(world.Layout, zones, favorite.Key, world.PetWidth);
            if (resolution is null)
            {
                logger?.LogDebug("[Favorite] Name={Name} unreachable: not on the taskbar or no free space", favorite.Key);
                continue;
            }

            logger?.LogDebug("[Favorite] Name={Name} DesiredPosition={Desired:F0} ActualDestination={Actual:F0} Zone={Zone} Reason={Reason}",
                favorite.Key, resolution.Desired, resolution.Destination, resolution.Zone, resolution.Reason ?? "-");
            resolved.Add((favorite.Key, resolution.Destination, Math.Max(0.1, favorite.Weight)));
        }

        key = null;
        if (resolved.Count == 0)
        {
            return null;
        }

        var roll = random.NextDouble() * resolved.Sum(r => r.Weight);
        foreach (var item in resolved)
        {
            if (roll < item.Weight)
            {
                key = item.Key;
                return item.X;
            }

            roll -= item.Weight;
        }

        key = resolved[^1].Key;
        return resolved[^1].X;
    }

    private Zone PickZone(IReadOnlyList<Zone> zones)
    {
        var roll = random.NextDouble() * zones.Sum(z => z.Width);
        foreach (var zone in zones)
        {
            if (roll < zone.Width)
            {
                return zone;
            }

            roll -= zone.Width;
        }

        return zones[^1];
    }

    private BehaviorPlan PlanSleeping(IReadOnlyList<Zone> zones)
    {
        double? spot = null;
        var clock = TaskbarGeometry.FindElement(world.Layout, "Clock");
        if (clock is not null)
        {
            spot = TaskbarGeometry.SpotBeside(zones, clock, world.PetWidth);
        }
        else if (zones.Count > 0)
        {
            var largest = zones.MaxBy(z => z.Width);
            spot = largest.Left + (largest.Width - world.PetWidth) / 2;
        }

        double? target = spot is { } s && Math.Abs(s - world.PetX) > 4 ? s : null;
        return new BehaviorPlan(Sleeping, PetState.Sleeping, Variant("sleep", PetActivity.Sleep), Range(30, 90), target, 0.8, target >= world.PetX);
    }

    private BehaviorPlan? PlanObserving(IReadOnlyList<Zone> zones)
    {
        var apps = world.Layout.Elements.Where(e => e.Kind == TaskbarElementKind.App).ToList();
        if (apps.Count == 0)
        {
            return null;
        }

        var weights = apps.Select(a => (App: a, Weight: (a.IsRunning ? 3.0 : 1.0) * (IsBrowser(a.Name) ? 2.0 : 1.0))).ToList();
        var roll = random.NextDouble() * weights.Sum(w => w.Weight);
        var chosen = weights[^1].App;
        foreach (var (app, weight) in weights)
        {
            if (roll < weight)
            {
                chosen = app;
                break;
            }

            roll -= weight;
        }

        if (TaskbarGeometry.ResolveNear(world.Layout, zones, chosen, "Observe:" + chosen.Name, world.PetWidth) is not { } resolution)
        {
            return null;
        }

        var spot = resolution.Destination;

        var faceRight = (chosen.Left + chosen.Right) / 2 >= spot + world.PetWidth / 2;
        return new BehaviorPlan(Observing, PetState.Idle, Variant("observe", PetActivity.Idle), Range(5, 12) * DwellScale, spot, 1.0, faceRight);
    }

    private static bool IsBrowser(string name) =>
        new[] { "Edge", "Chrome", "Firefox", "Brave", "Opera" }.Any(b => name.Contains(b, StringComparison.OrdinalIgnoreCase));

    private BehaviorPlan? PlanPlaying(IReadOnlyList<Zone> zones)
    {
        var zone = PickZone(zones);
        var target = zone.Left + random.NextDouble() * Math.Max(0, zone.Width - world.PetWidth);
        return new BehaviorPlan(Playing, PetState.Happy, Variant("play", PetActivity.Happy), Range(3, 6), target, 1.8, target >= world.PetX);
    }

    private BehaviorPlan? PlanFollowing(IReadOnlyList<Zone> zones)
    {
        if (world.CursorX is not { } cursor)
        {
            return null;
        }

        var x = TaskbarGeometry.ClampToNearestZone(zones, cursor - world.PetWidth / 2, world.PetWidth);
        return new BehaviorPlan(Following, PetState.Idle, Variant("sit", PetActivity.Sit), Range(4, 8), x, 1.3, cursor >= x + world.PetWidth / 2);
    }
}
