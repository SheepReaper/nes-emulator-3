using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EmuSheep;

public sealed partial class MainPage : Page
{
    private async Task StartRomAsync(string fileName, byte[] romData)
    {
        var replacement = new NesEmulationSession(romData);
        replacement.AudioUnavailable += Session_AudioUnavailable;
        await replacement.InitializeAudioAsync();
        await StopAndDisposeSessionAsync();
        if (_isUnloaded)
        {
            await replacement.DisposeAsync();
            return;
        }

        replacement.FrameAvailable += Session_FrameAvailable;
        replacement.FrameRateAvailable += Session_FrameRateAvailable;
        replacement.Faulted += Session_Faulted;
        replacement.SetControllerState(((App)Application.Current).MainWindow.ControllerButtons);
        _session = replacement;
        _romFileName = fileName;
        UpdateUiOnStart(replacement, fileName);
        replacement.Start();
        ((App)Application.Current).MainWindow.ControllerInputEnabled = true;
    }

    private void UpdateUiOnStart(NesEmulationSession session, string fileName)
    {
        EmptyState.Visibility = Visibility.Collapsed;
        StatusText.Text = $"Running · {fileName}";
        FrameRateText.Text = "Measuring…";
        MuteButton.IsEnabled = session.HasAudio;
        VolumeSlider.IsEnabled = session.HasAudio;
        FilterSelector.IsEnabled = true;
        session.SetMuted(MuteButton.IsChecked == true);
        session.SetVolume(VolumeSlider.Value / 100.0);
        session.SetFilterMode(FilterSelector.SelectedIndex == 1 ? NesAudioFilterMode.Raw : NesAudioFilterMode.Nes);
    }

    private void SetLoadingState(string fileName)
    {
        OpenRomButton.IsEnabled = false;
        LoadingIndicator.IsActive = true;
        StatusText.Text = $"Loading · {fileName}";
        FrameRateText.Text = "— FPS";
    }

    private void ShowError(string message)
    {
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
        StatusText.Text = _session == null ? "No ROM loaded" : $"Running · {_romFileName}";
    }

    private void Session_AudioUnavailable(object? sender, EmulationFaultedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AudioInfoBar.Message = $"{e.Exception.Message} The game will continue silently.";
            AudioInfoBar.IsOpen = true;
        });
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e) =>
        _session?.SetMuted(MuteButton.IsChecked == true);

    private void VolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) =>
        _session?.SetVolume(e.NewValue / 100.0);

    private void FilterSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _session?.SetFilterMode(FilterSelector.SelectedIndex == 1 ? NesAudioFilterMode.Raw : NesAudioFilterMode.Nes);
}
