using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EmuSheep;

public sealed partial class MainPage : Page
{
    private void Session_FrameAvailable(object? sender, EventArgs e)
    {
        if (_isUnloaded || Interlocked.Exchange(ref _presentationQueued, 1) != 0)
        {
            return;
        }

        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, PresentLatestFrame))
        {
            Interlocked.Exchange(ref _presentationQueued, 0);
        }
    }

    private void Session_FrameRateAvailable(object? sender, FrameRateAvailableEventArgs e)
    {
        if (_isUnloaded)
        {
            return;
        }
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ReferenceEquals(sender, _session))
            {
                FrameRateText.Text = $"{e.FramesPerSecond:F1} FPS";
            }
        });
    }

    private void Session_Faulted(object? sender, EmulationFaultedEventArgs e)
    {
        if (_isUnloaded)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (!ReferenceEquals(sender, _session))
            {
                return;
            }

            ErrorInfoBar.Message = e.Exception.Message;
            ErrorInfoBar.IsOpen = true;
            StatusText.Text = $"Stopped · {_romFileName}";
            ((App)Application.Current).MainWindow.ControllerInputEnabled = false;
        });
    }

    private void PresentLatestFrame()
    {
        try
        {
            if (_isUnloaded || _session == null)
            {
                return;
            }

            _presenter.Present(_session);
        }
        finally
        {
            Interlocked.Exchange(ref _presentationQueued, 0);
        }
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        Focus(FocusState.Programmatic);
        ((App)Application.Current).MainWindow.ControllerStateChanged += MainWindow_ControllerStateChanged;
    }

    private void MainWindow_ControllerStateChanged(NesControllerButton buttons) =>
        _session?.SetControllerState(buttons);

    private async void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isUnloaded = true;
        ((App)Application.Current).MainWindow.ControllerStateChanged -= MainWindow_ControllerStateChanged;
        await StopAndDisposeSessionAsync();
        _presenter.Dispose();
    }

    private async Task StopAndDisposeSessionAsync()
    {
        var session = _session;
        if (session == null)
        {
            return;
        }

        _session = null;
        ((App)Application.Current).MainWindow.ControllerInputEnabled = false;
        session.FrameAvailable -= Session_FrameAvailable;
        session.FrameRateAvailable -= Session_FrameRateAvailable;
        session.Faulted -= Session_Faulted;
        session.AudioUnavailable -= Session_AudioUnavailable;
        await session.DisposeAsync();
    }
}
