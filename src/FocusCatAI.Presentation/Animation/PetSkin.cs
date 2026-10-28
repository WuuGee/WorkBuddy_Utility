using System.Windows.Media;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pets;

namespace FocusCatAI.Presentation.Animation;

public sealed record SpriteClip(IReadOnlyList<ImageSource> Frames, double Fps, bool Loop, bool FlipHorizontal);

public sealed class PetSkin(
    PetManifest manifest,
    IReadOnlyDictionary<string, SpriteClip> clips,
    IReadOnlyDictionary<string, IReadOnlyList<PetActivity>> tags) : IAnimationCatalog
{
    public PetManifest Manifest { get; } = manifest;

    public bool Has(PetActivity activity) => clips.ContainsKey(activity.Name);

    public IReadOnlyList<PetActivity> WithTag(string tag) =>
        tags.TryGetValue(tag, out var list) ? list : [];

    public SpriteClip GetClip(PetActivity activity) =>
        clips.TryGetValue(activity.Name, out var clip) ? clip : clips[PetActivity.Idle.Name];
}
