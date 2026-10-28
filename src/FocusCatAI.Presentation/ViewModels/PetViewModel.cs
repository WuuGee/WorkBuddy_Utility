using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Domain.Taskbar;
using FocusCatAI.Presentation.Animation;
using FocusCatAI.Presentation.Configuration;
using FocusCatAI.Presentation.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FocusCatAI.Presentation.ViewModels;

public sealed partial class PetViewModel : ObservableObject
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    private readonly IPetStateMachine _stateMachine;
    private readonly IPetWorld _world;
    private readonly ITaskbarLayoutProvider _layoutProvider;
    private readonly IPetPreferences _preferences;
    private readonly IReadOnlyList<ITickable> _tickables;
    private readonly PetOptions _options;
    private readonly ISettingsService _settings;
    private readonly ILogger<PetViewModel> _logger;
    private readonly SpriteAnimator _animator = new();
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    private PetSkin _skin;
    private TimeSpan _lastTick;
    private TimeSpan _sinceTopmost;
    private TimeSpan _sinceSave;
    private TimeSpan _sinceDebugLog;
    private bool _dragging;
    private bool _placed;
    private PetSize _size;
    private double _lastLeft = double.NaN, _lastTop = double.NaN;

    [ObservableProperty] private ImageSource? _currentFrame;
    [ObservableProperty] private double _opacity;
    [ObservableProperty] private double _width = 64;
    [ObservableProperty] private double _height = 64;
    [ObservableProperty] private bool _flipped;

    public PetViewModel(
        IPetStateMachine stateMachine,
        IPetWorld world,
        ITaskbarLayoutProvider layoutProvider,
        IPetPreferences preferences,
        ISpriteManager sprites,
        IEnumerable<ITickable> tickables,
        IOptions<PetOptions> options,
        ISettingsService settings,
        ILogger<PetViewModel> logger)
    {
        _settings = settings;
        _stateMachine = stateMachine;
        _world = world;
        _layoutProvider = layoutProvider;
        _preferences = preferences;
        _tickables = tickables.ToList();
        _options = options.Value;
        _logger = logger;
        _opacity = _options.Opacity;
        _size = settings.Current.Pet.Size;
        _skin = sprites.Load(_options.PetId);

        _stateMachine.StateChanged += (_, _) => PlayCurrentActivity();
        ApplyLayout();
        PlayCurrentActivity();

        _timer = new DispatcherTimer(FrameInterval, DispatcherPriority.Render, OnTick, Dispatcher.CurrentDispatcher)
        {
            IsEnabled = false
        };
    }

    public double Left { get; private set; }
    public TaskbarLayout Layout => _world.Layout;
    public int FrameIndex => _animator.FrameIndex;
    public string ActivityName => _stateMachine.Activity.Name;
    public double Top { get; private set; }
    public TaskbarPlacementMode Mode => _world.Layout.Mode;
    public PetSkin Skin => _skin;
    public event EventHandler? PositionChanged;

    /// <summary>Raised each frame with whether the pet can currently receive mouse input.</summary>
    public event EventHandler<bool>? HitTestChanged;

    /// <summary>Animation speed multiplier currently used by the animation loop (read live from settings).</summary>
    public double AnimationSpeed => _settings.Current.Pet.AnimationSpeed;

    public void SetSize(PetSize size)
    {
        _size = size;
        ApplyLayout();
    }

    public void Start()
    {
        _clock.Restart();
        _lastTick = TimeSpan.Zero;
        _timer.Start();
        _logger.LogInformation("Pet started");
    }

    public void Stop()
    {
        _timer.Stop();
        _preferences.Save();
    }

    public void BeginDrag()
    {
        _dragging = true;
        _stateMachine.Trigger(PetTrigger.UserInteracted);
    }

    public void EndDrag(double left)
    {
        _dragging = false;
        _world.PetX = left;
        _lastLeft = double.NaN;
        _placed = true;
        SnapToZone();
    }

    public void Celebrate() => _stateMachine.Trigger(PetTrigger.Celebrate);

    private void ApplyLayout()
    {
        var layout = _layoutProvider.Current;
        _world.Layout = layout;
        _size = _settings.Current.Pet.Size;

        var size = Application.Taskbar.TaskbarFit.ComputeSize(layout, (double)(int)_size);

        if (Math.Abs(size - _world.PetHeight) > 0.01)
        {
            _world.PetWidth = size;
            _world.PetHeight = size;
            Width = size;
            Height = size;
            _lastLeft = double.NaN;
        }

        _world.WalkSpeed = _skin.Manifest.WalkSpeed * size / 64.0;

        if (!_placed && layout.Width > 0)
        {
            _world.PetX = layout.Left + layout.Width * 0.5;
            _placed = true;
            SnapToZone();
        }
    }

    private void SnapToZone()
    {
        var zones = Application.Taskbar.TaskbarGeometry.GetZones(_world.Layout, _world.PetWidth);
        if (zones.Count > 0)
        {
            _world.PetX = Application.Taskbar.TaskbarGeometry.ClampToNearestZone(zones, _world.PetX, _world.PetWidth);
        }
    }

    private void PlayCurrentActivity()
    {
        var clip = _skin.GetClip(_stateMachine.Activity);
        _animator.Play(clip);
        CurrentFrame = _animator.CurrentFrame;
        Flipped = clip.FlipHorizontal;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var dt = now - _lastTick;
        _lastTick = now;
        if (dt > TimeSpan.FromSeconds(1))
        {
            dt = FrameInterval;
        }

        ApplyLayout();
        UpdateCursor();

        if (!_dragging)
        {
            foreach (var tickable in _tickables)
            {
                tickable.Tick(dt);
            }
        }

        _animator.Advance(dt, _settings.Current.Pet.AnimationSpeed);
        CurrentFrame = _animator.CurrentFrame;

        if (!_dragging)
        {
            UpdatePosition();
            UpdateHitTest();
        }

        if (_options.DebugRenderMode && (_sinceDebugLog += dt) > TimeSpan.FromSeconds(5))
        {
            _sinceDebugLog = TimeSpan.Zero;
            var crop = (CurrentFrame as CroppedBitmap)?.SourceRect;
            _logger.LogDebug("Render: activity={Activity} crop={Crop} pet=({L:F0},{T:F0}) {W:F0}x{H:F0} opacity={Opacity} mode={Mode} bar=({BL:F0},{BT:F0},{BR:F0},{BB:F0})",
                _stateMachine.Activity, crop, Left, Top, Width, Height, Opacity, Mode,
                _world.Layout.Left, _world.Layout.Top, _world.Layout.Right, _world.Layout.Bottom);
        }

        _sinceTopmost += dt;
        if (_sinceTopmost > TimeSpan.FromSeconds(2))
        {
            _sinceTopmost = TimeSpan.Zero;
            TopmostRequested?.Invoke(this, EventArgs.Empty);
        }

        _sinceSave += dt;
        if (_sinceSave > TimeSpan.FromMinutes(5))
        {
            _sinceSave = TimeSpan.Zero;
            _preferences.Save();
        }
    }

    public event EventHandler? TopmostRequested;

    private void UpdatePosition()
    {
        var layout = _world.Layout;
        var groundPx = layout.Mode == TaskbarPlacementMode.InTaskbar
            ? 0
            : _skin.Manifest.GroundOffset * _world.PetHeight / _skin.Manifest.FrameHeight;
        var top = Application.Taskbar.TaskbarFit.ComputeTop(layout, _world.PetHeight) + groundPx;
        var left = _world.PetX;
        if (_options.DebugForcePosition)
        {
            left = 500;
            top = 500;
        }

        _world.PetTop = top;
        if (Math.Abs(left - _lastLeft) > 0.25 || Math.Abs(top - _lastTop) > 0.25 || double.IsNaN(_lastLeft))
        {
            _lastLeft = left;
            _lastTop = top;
            Left = left;
            Top = top;
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateCursor()
    {
        _world.CursorX = null;
        var layout = _world.Layout;
        if (layout.Mode != TaskbarPlacementMode.InTaskbar || !Native.GetCursorPos(out var p))
        {
            return;
        }

        var scale = Native.DpiScale;
        var x = p.X / scale;
        var y = p.Y / scale;
        if (y >= layout.Top && y <= layout.Bottom && x >= layout.Left && x <= layout.Right)
        {
            _world.CursorX = x;
        }
    }

    /// <summary>
    /// In taskbar mode the window is click-through unless the cursor is on the pet AND the pet is not covering
    /// a taskbar button, so taskbar clicks are never blocked.
    /// </summary>
    private void UpdateHitTest()
    {
        var layout = _world.Layout;
        bool interactive;
        if (layout.Mode == TaskbarPlacementMode.AboveTaskbar)
        {
            interactive = true;
        }
        else
        {
            var coversButton = layout.Elements.Any(el =>
                el.Right > _world.PetX + 2 && el.Left < _world.PetX + _world.PetWidth - 2);
            interactive = !coversButton && IsCursorOverPet();
        }

        HitTestChanged?.Invoke(this, interactive);
    }

    private bool IsCursorOverPet()
    {
        if (!Native.GetCursorPos(out var p))
        {
            return false;
        }

        var scale = Native.DpiScale;
        var x = p.X / scale;
        var y = p.Y / scale;
        return x >= Left && x <= Left + Width && y >= Top && y <= Top + Height;
    }
}
