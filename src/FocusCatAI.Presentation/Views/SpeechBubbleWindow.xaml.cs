using System.Windows;
using System.Windows.Interop;
using FocusCatAI.Presentation.Interop;

namespace FocusCatAI.Presentation.Views;

/// <summary>Small click-through bubble shown briefly above the pet. Never takes focus or blocks input.</summary>
public partial class SpeechBubbleWindow : Window
{
    public SpeechBubbleWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
            Native.SetExStyle(new WindowInteropHelper(this).Handle,
                Native.WsExToolWindow | Native.WsExNoActivate | Native.WsExTransparent, true);
    }

    public void SetText(string text) => Message.Text = text;
}
