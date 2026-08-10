using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SR.Emulation.Nes;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace EmuSheep;

/// <summary>
/// The main content page displayed inside the application window.
/// Add your UI logic, event handlers, and data binding here.
/// </summary>
public sealed partial class MainPage : Page
{
    private const ulong MaximumRomFileSize = 1024 * 1024;

    private readonly WriteableBitmap _frameBitmap = new(Nes.FrameWidth, Nes.FrameHeight);
    private readonly byte[] _rgbaFrame = new byte[Nes.FrameBufferSize];
    private readonly byte[] _bgraFrame = new byte[Nes.FrameBufferSize];
    private readonly Stream _pixelBufferStream;

    private NesEmulationSession? _session;
    private string? _romFileName;
    private int _presentationQueued;
    private volatile bool _isUnloaded;

    public MainPage()
    {
        InitializeComponent();
        _pixelBufferStream = _frameBitmap.PixelBuffer.AsStream();
        NesDisplay.Source = _frameBitmap;
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string startupRomPath)
        {
            await LoadRomFromPathAsync(startupRomPath);
        }
    }

    private async void OpenRomButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorInfoBar.IsOpen = false;
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add(".nes");

            var window = ((App)Application.Current).MainWindow;
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

            var file = await picker.PickSingleFileAsync();
            if (file == null || _isUnloaded)
            {
                return;
            }

            SetLoadingState(file.Name);
            var fileProperties = await file.GetBasicPropertiesAsync();
            if (fileProperties.Size > MaximumRomFileSize)
            {
                throw new InvalidDataException("The selected ROM is larger than the supported 1 MB limit.");
            }

            byte[] romData;
            await using (var fileStream = await file.OpenStreamForReadAsync())
            {
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                romData = memoryStream.ToArray();
            }

            await StartRomAsync(file.Name, romData);
        }
        catch (Exception exception)
        {
            ErrorInfoBar.Message = exception.Message;
            ErrorInfoBar.IsOpen = true;
            StatusText.Text = _session == null ? "No ROM loaded" : $"Running · {_romFileName}";
        }
        finally
        {
            OpenRomButton.IsEnabled = true;
            LoadingIndicator.IsActive = false;
        }
    }

    private async Task LoadRomFromPathAsync(string romPath)
    {
        ErrorInfoBar.IsOpen = false;
        try
        {
            var fullPath = Path.GetFullPath(romPath);
            if (!string.Equals(Path.GetExtension(fullPath), ".nes", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The startup ROM must be an iNES .nes file.");

            var file = new FileInfo(fullPath);
            if (!file.Exists)
                throw new FileNotFoundException("The startup ROM file was not found.", fullPath);
            if ((ulong)file.Length > MaximumRomFileSize)
                throw new InvalidDataException("The startup ROM is larger than the supported 1 MB limit.");

            SetLoadingState(file.Name);
            await StartRomAsync(file.Name, await File.ReadAllBytesAsync(fullPath));
        }
        catch (Exception exception)
        {
            ErrorInfoBar.Message = exception.Message;
            ErrorInfoBar.IsOpen = true;
            StatusText.Text = _session == null ? "No ROM loaded" : $"Running · {_romFileName}";
        }
        finally
        {
            OpenRomButton.IsEnabled = true;
            LoadingIndicator.IsActive = false;
        }
    }

    private async Task StartRomAsync(string fileName, byte[] romData)
    {
        var replacement = new NesEmulationSession(romData);
        await StopAndDisposeSessionAsync();
        if (_isUnloaded)
        {
            await replacement.DisposeAsync();
            return;
        }

        replacement.FrameAvailable += Session_FrameAvailable;
        replacement.Faulted += Session_Faulted;
        replacement.SetControllerState(((App)Application.Current).MainWindow.ControllerButtons);
        _session = replacement;
        _romFileName = fileName;
        EmptyState.Visibility = Visibility.Collapsed;
        StatusText.Text = $"Running · {fileName}";
        replacement.Start();
    }

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
        });
    }

    private void PresentLatestFrame()
    {
        try
        {
            if (_isUnloaded || _session?.TryCopyLatestFrame(_rgbaFrame, out _) != true)
            {
                return;
            }

            RgbaToBgraConverter.Convert(_rgbaFrame, _bgraFrame);
            _pixelBufferStream.Position = 0;
            _pixelBufferStream.Write(_bgraFrame);
            _frameBitmap.Invalidate();
        }
        finally
        {
            Interlocked.Exchange(ref _presentationQueued, 0);
        }
    }

    private void SetLoadingState(string fileName)
    {
        OpenRomButton.IsEnabled = false;
        LoadingIndicator.IsActive = true;
        StatusText.Text = $"Loading · {fileName}";
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
        _pixelBufferStream.Dispose();
    }

    private async Task StopAndDisposeSessionAsync()
    {
        var session = _session;
        if (session == null)
        {
            return;
        }

        _session = null;
        session.FrameAvailable -= Session_FrameAvailable;
        session.Faulted -= Session_Faulted;
        await session.DisposeAsync();
    }
}
