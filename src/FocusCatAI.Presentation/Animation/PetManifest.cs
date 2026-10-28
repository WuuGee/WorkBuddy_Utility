using System.Text.Json.Serialization;

namespace FocusCatAI.Presentation.Animation;

/// <summary>Deserialized pet.json. See Assets/README.md for the full specification.</summary>
public sealed class PetManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int FrameWidth { get; set; } = 64;
    public int FrameHeight { get; set; } = 64;

    /// <summary>Transparent pixel rows at the bottom of each frame (at native size) so feet rest on the taskbar edge.</summary>
    public int GroundOffset { get; set; }

    /// <summary>Walking speed in DIPs per second at native (64 px) size. Scales with the pet size.</summary>
    public double WalkSpeed { get; set; } = 40;

    public Dictionary<string, AnimationDefinition> Animations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AnimationDefinition
{
    public string Sheet { get; set; } = "";
    public int Frames { get; set; } = 1;
    public double Fps { get; set; } = 6;
    public bool Loop { get; set; } = true;

    [JsonPropertyName("flipHorizontal")]
    public bool FlipHorizontal { get; set; }

    /// <summary>Behavior hooks: the planner picks randomly among animations that carry a tag (idle, sit, sleep, stretch, observe, play, ...).</summary>
    public List<string> Tags { get; set; } = [];
}
