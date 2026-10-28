using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Presentation.Animation;

namespace FocusCatAI.Presentation.Views;

/// <summary>
/// Troubleshooting window that bypasses all taskbar logic. Mode 1: red 100x100 with lime border.
/// Mode 2: adds a blue 64x64 rectangle. Mode 3: adds the first Idle cat frame instead of the rectangle.
/// </summary>
public sealed class DebugTestWindow : Window
{
    public DebugTestWindow(int mode, ISpriteManager sprites, string petId)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 500;
        Top = 500;
        Width = 100;
        Height = 100;
        Opacity = 1.0;
        Background = Brushes.Red;

        var grid = new Grid();
        UIElement content = mode switch
        {
            >= 3 => new Image
            {
                Source = sprites.Load(petId).GetClip(PetActivity.Idle).Frames[0],
                Width = 64,
                Height = 64,
                Stretch = Stretch.Fill
            },
            2 => new System.Windows.Shapes.Rectangle { Width = 64, Height = 64, Fill = Brushes.Blue },
            _ => new Canvas()
        };
        RenderOptions.SetBitmapScalingMode(content, BitmapScalingMode.NearestNeighbor);
        grid.Children.Add(content);

        Content = new Border { BorderBrush = Brushes.Lime, BorderThickness = new Thickness(2), Child = grid };
    }
}
