using System.Windows.Media;

namespace FocusCatAI.Presentation.Animation;

/// <summary>Advances frames of a single clip. Driven externally by the pet's single render timer.</summary>
public sealed class SpriteAnimator
{
    private SpriteClip? _clip;
    private double _position;

    public ImageSource? CurrentFrame { get; private set; }
    public int FrameIndex { get; private set; }
    public bool FlipHorizontal => _clip?.FlipHorizontal ?? false;

    public void Play(SpriteClip clip)
    {
        if (ReferenceEquals(_clip, clip))
        {
            return;
        }

        _clip = clip;
        _position = 0;
        FrameIndex = 0;
        CurrentFrame = clip.Frames[0];
    }

    public void Advance(TimeSpan elapsed, double speedMultiplier)
    {
        if (_clip is null)
        {
            return;
        }

        _position += elapsed.TotalSeconds * _clip.Fps * speedMultiplier;
        var count = _clip.Frames.Count;
        var index = _clip.Loop ? (int)_position % count : Math.Min((int)_position, count - 1);
        CurrentFrame = _clip.Frames[index];
        FrameIndex = index;
    }
}
