using System.Text.Json;

namespace FocusCatAI.Domain.Accessories;

/// <summary>Per-frame anchor in 64x64 sprite-frame pixels: the forehead point between the ears and a point on the chin/neck.</summary>
public readonly record struct AccessoryAnchor(int HeadX, int HeadY, int NeckX, int NeckY);

/// <summary>Where an accessory sprite's top-left corner goes inside the 64x64 frame, or that it is hidden for this frame.</summary>
public readonly record struct AccessoryPlacement(bool Visible, int X, int Y)
{
    public static AccessoryPlacement Hidden { get; } = new(false, 0, 0);
}

/// <summary>Per-frame anchors measured from the pet sheets (Assets\Accessories\anchors.json, produced by tools\Generate-AccessoryAnchors.ps1).</summary>
public sealed class AccessoryAnchorTable
{
    private readonly Dictionary<string, AccessoryAnchor[]> _animations;

    private AccessoryAnchorTable(Dictionary<string, AccessoryAnchor[]> animations) => _animations = animations;

    public IEnumerable<string> Animations => _animations.Keys;

    public int FrameCount(string animation) => _animations.TryGetValue(animation, out var frames) ? frames.Length : 0;

    /// <summary>The anchor for a frame; the index wraps so a longer animation can never read out of range. Null for an unknown animation.</summary>
    public AccessoryAnchor? Get(string animation, int frame) =>
        _animations.TryGetValue(animation, out var frames) && frames.Length > 0
            ? frames[((frame % frames.Length) + frames.Length) % frames.Length]
            : null;

    public static AccessoryAnchorTable Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var doc = JsonDocument.Parse(json);
        var map = new Dictionary<string, AccessoryAnchor[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var animation in doc.RootElement.GetProperty("animations").EnumerateObject())
        {
            map[animation.Name] = animation.Value.EnumerateArray()
                .Select(f => new AccessoryAnchor(f.GetProperty("hx").GetInt32(), f.GetProperty("hy").GetInt32(), f.GetProperty("nx").GetInt32(), f.GetProperty("ny").GetInt32()))
                .ToArray();
        }

        return new AccessoryAnchorTable(map);
    }
}

/// <summary>
/// Pure placement rules. Everything is in unflipped 64x64 frame coordinates; mirroring for WalkLeft is done once, by the
/// renderer's shared parent transform, so nothing here flips anything. The result is always clamped inside the frame.
/// </summary>
public static class AccessoryPlacer
{
    public const int FrameSize = 64;

    public static AccessoryPlacement Place(string accessoryId, string animation, AccessoryAnchor anchor, int width, int height)
    {
        var asleep = string.Equals(animation, "Sleep", StringComparison.OrdinalIgnoreCase);
        int x, y;
        switch (accessoryId)
        {
            case "bowtie" when !asleep:
                x = anchor.NeckX - width / 2;
                y = anchor.NeckY - height / 2;
                break;
            case "party_hat" when !asleep:
                // Rests on the forehead between the ears; one row overlaps the head so it never hovers.
                x = anchor.HeadX - width / 2;
                y = anchor.HeadY + 1 - height;
                break;
            case "star_halo":
                x = anchor.HeadX - width / 2;
                y = anchor.HeadY - 1 - height;
                break;
            default:
                return AccessoryPlacement.Hidden;
        }

        // The ears reach y = 1 in Happy, so a hat/halo above the forehead can need to move down a little: never clip.
        return new AccessoryPlacement(true, Math.Clamp(x, 0, FrameSize - width), Math.Clamp(y, 0, FrameSize - height));
    }
}
