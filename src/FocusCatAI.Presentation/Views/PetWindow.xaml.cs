using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FocusCatAI.Presentation.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FocusCatAI.Domain.Taskbar;
using FocusCatAI.Presentation.Interop;
using FocusCatAI.Presentation.ViewModels;

namespace FocusCatAI.Presentation.Views;

public partial class PetWindow : Window
{
    private readonly PetViewModel _viewModel;
    private IntPtr _hwnd;
    private bool _clickThrough;
    private ZOrderGuard? _zGuard;

    public PetWindow(PetViewModel viewModel, IOptions<PetOptions> options, ILogger<PetWindow> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        if (options.Value.DebugRenderMode)
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 255, 0, 0));
            BorderBrush = Brushes.Lime;
            BorderThickness = new Thickness(1);
        }

        ContentRendered += (_, _) =>
            logger.LogInformation(
                "PetWindow rendered: visibility={Visibility} visible={IsVisible} size={W:F0}x{H:F0} location=({L:F0},{T:F0}) opacity={Opacity} topmost={Topmost} imageOpacity={ImageOpacity}",
                Visibility, IsVisible, ActualWidth, ActualHeight, Left, Top, Opacity, Topmost, (Content as UIElement)?.Opacity);

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Native.SetExStyle(_hwnd, Native.WsExToolWindow | Native.WsExNoActivate, true);
            _zGuard = new ZOrderGuard(_hwnd, logger);
        };
        Closed += (_, _) => _zGuard?.Dispose();
        Loaded += (_, _) =>
        {
            Move();
            viewModel.Start();
            StartDiagnostics(logger);
        };
        Closed += (_, _) => viewModel.Stop();

        viewModel.PositionChanged += (_, _) => Move();
        viewModel.HitTestChanged += (_, interactive) => SetClickThrough(!interactive);
        viewModel.TopmostRequested += (_, _) => _zGuard?.FallbackCheck();
    }

    private void Move()
    {
        // Size is set explicitly: the XAML Height binding left the window 1023 px tall (see runtime log), so the
        // window extended far below the taskbar and off-screen. Compare against ActualHeight because Height can be NaN.
        if (Math.Abs(ActualWidth - _viewModel.Width) > 0.01)
        {
            Width = _viewModel.Width;
        }

        if (Math.Abs(ActualHeight - _viewModel.Height) > 0.01)
        {
            Height = _viewModel.Height;
        }

        Left = _viewModel.Left;
        Top = _viewModel.Top;
    }

    private void StartDiagnostics(ILogger logger)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            try
            {
                LogDiagnostics(logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pet diagnostics failed");
            }
        };
        timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    private void LogDiagnostics(ILogger logger)
    {
        var frame = _viewModel.CurrentFrame as System.Windows.Media.Imaging.BitmapSource;
        var crop = (_viewModel.CurrentFrame as System.Windows.Media.Imaging.CroppedBitmap)?.SourceRect;
        Native.GetWindowRect(_hwnd, out var rect);
        var layout = _viewModel.Layout;
        var scale = Native.DpiScale;
        var tray = Native.FindWindow("Shell_TrayWnd", null);

        logger.LogInformation(
            "PET WINDOW hwnd=0x{Hwnd:X} visible={Visible} loaded={Loaded} actual={AW:F0}x{AH:F0} wpf=({L:F0},{T:F0}) opacity={Op} topmost={Top} win32Rect=({RL},{RT},{RR},{RB}) style=0x{Style:X} exStyle=0x{Ex:X} dpiScale={Dpi}",
            _hwnd.ToInt64(), IsVisible, IsLoaded, ActualWidth, ActualHeight, Left, Top, Opacity, Topmost,
            rect.Left, rect.Top, rect.Right, rect.Bottom, Native.GetStyle(_hwnd, false), Native.GetStyle(_hwnd, true), scale);

        logger.LogInformation(
            "PET IMAGE actual={W:F0}x{H:F0} visibility={Vis} opacity={Op} activity={Activity} frameIndex={Index} bitmap={BW}x{BH} crop={Crop} sourceNull={Null}",
            PetImage.ActualWidth, PetImage.ActualHeight, PetImage.Visibility, PetImage.Opacity, _viewModel.ActivityName,
            _viewModel.FrameIndex, frame?.PixelWidth, frame?.PixelHeight, crop, PetImage.Source is null);

        logger.LogInformation(
            "TASKBAR mode={Mode} rectDip=({L:F0},{T:F0},{R:F0},{B:F0}) rectPx=({PL:F0},{PT:F0},{PR:F0},{PB:F0}) petRectDip=({XL:F0},{XT:F0},{XR:F0},{XB:F0}) petInsideBar={Inside}",
            layout.Mode, layout.Left, layout.Top, layout.Right, layout.Bottom,
            layout.Left * scale, layout.Top * scale, layout.Right * scale, layout.Bottom * scale,
            Left, Top, Left + ActualWidth, Top + ActualHeight,
            Top >= layout.Top - 0.5 && Top + ActualHeight <= layout.Bottom + 0.5);

        var petZ = Native.ZIndex(_hwnd);
        var trayZ = tray == IntPtr.Zero ? -1 : Native.ZIndex(tray);
        logger.LogInformation("Z-ORDER above=[{Above}] below=[{Below}] petZ={PetZ} trayZ={TrayZ}",
            Native.Describe(Native.WindowAbove(_hwnd)), Native.Describe(Native.WindowBelow(_hwnd)), petZ, trayZ);

        if (trayZ < 0)
        {
            return;
        }

        if (trayZ < petZ)
        {
            logger.LogWarning("PetWindow is behind taskbar (petZ={PetZ}, trayZ={TrayZ}); forcing to top of topmost band", petZ, trayZ);
            _zGuard?.FallbackCheck();
            logger.LogWarning("Z-ORDER after fix: above=[{Above}] petZ={PetZ} trayZ={TrayZ}",
                Native.Describe(Native.WindowAbove(_hwnd)), Native.ZIndex(_hwnd), Native.ZIndex(tray));
        }
        else
        {
            logger.LogInformation("PetWindow is above taskbar (ZOrderGuardCorrections={Guard}, FallbackCorrections={Fallback})",
                _zGuard?.ZOrderGuardCorrections, _zGuard?.FallbackCorrections);
        }
    }

    private void SetClickThrough(bool enabled)
    {
        if (enabled == _clickThrough)
        {
            return;
        }

        _clickThrough = enabled;
        Native.SetExStyle(_hwnd, Native.WsExTransparent, enabled);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var startLeft = Left;
        var startTop = Top;
        _viewModel.BeginDrag();
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }

        var clicked = Math.Abs(Left - startLeft) < 3 && Math.Abs(Top - startTop) < 3;
        _viewModel.EndDrag(Left);
        if (clicked)
        {
            _viewModel.Celebrate();
        }
    }
}
