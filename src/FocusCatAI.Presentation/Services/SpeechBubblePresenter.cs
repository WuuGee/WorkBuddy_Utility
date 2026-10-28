using System.Windows;
using System.Windows.Threading;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Presentation.Views;

namespace FocusCatAI.Presentation.Services;

/// <summary>Shows speech above the pet. The bubble is owned by, and positioned from, the pet window itself.</summary>
public sealed class SpeechBubblePresenter(IPetStateMachine pet) : ISpeechPresenter
{
    private SpeechBubbleWindow? _window;
    private DispatcherTimer? _hideTimer;
    private DispatcherTimer? _followTimer;

    private static PetWindow? PetWindow =>
        System.Windows.Application.Current.Windows.OfType<PetWindow>().FirstOrDefault(w => w.IsVisible);

    public void Show(string text, TimeSpan duration)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _window ??= new SpeechBubbleWindow();
            if (_window.Owner is null && PetWindow is { } owner)
            {
                _window.Owner = owner;
            }

            _window.SetText(text);
            _window.Show();
            _window.UpdateLayout();
            Reposition();

            _followTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background,
                (_, _) => Reposition(), Dispatcher.CurrentDispatcher);
            _followTimer.Start();

            _hideTimer?.Stop();
            _hideTimer = new DispatcherTimer(duration, DispatcherPriority.Normal, (_, _) => Hide(), Dispatcher.CurrentDispatcher);
            _hideTimer.Start();
        });
    }

    private void Reposition()
    {
        if (_window is null || PetWindow is not { } petWindow)
        {
            return;
        }

        var area = SystemParameters.WorkArea;
        var left = petWindow.Left + petWindow.ActualWidth / 2 - _window.ActualWidth / 2;
        _window.Left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - _window.ActualWidth));
        _window.Top = petWindow.Top - _window.ActualHeight - 4;
    }

    private void Hide()
    {
        _hideTimer?.Stop();
        _followTimer?.Stop();
        _window?.Hide();
        pet.Trigger(Domain.Pets.PetTrigger.SpeechFinished);
    }
}
