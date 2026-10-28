namespace FocusCatAI.Presentation.Configuration;

public sealed class PetOptions
{
    /// <summary>Folder (relative to the executable, or absolute) that contains Pets/, Skins/ and Accessories/.</summary>
    public string AssetsRoot { get; set; } = "Assets";

    public string PetId { get; set; } = "cat";
    public string FallbackPetId { get; set; } = "cat";

    /// <summary>Small, Normal, Large or Huge (32/48/64/96 px). Scaled down to fit the taskbar in taskbar mode.</summary>
    public string Size { get; set; } = "Normal";

    /// <summary>InTaskbar (default, falls back automatically) or AboveTaskbar.</summary>
    public string Placement { get; set; } = "InTaskbar";

    /// <summary>Seconds without keyboard/mouse input before the pet falls asleep. Lower it temporarily to test sleep.</summary>
    public int SleepDelaySeconds { get; set; } = 300;

    /// <summary>Troubleshooting: red background, bright border, and periodic render/window diagnostics in the log.</summary>
    public bool DebugRenderMode { get; set; }

    /// <summary>Troubleshooting: ignore taskbar placement and pin the pet at screen position 500,500.</summary>
    public bool DebugForcePosition { get; set; }

    /// <summary>Troubleshooting: 0 = off, 1 = red square, 2 = blue rectangle, 3 = first cat frame, all at 500,500 with no taskbar logic.</summary>
    public int DebugTestMode { get; set; }

    public double AnimationSpeed { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;
}
