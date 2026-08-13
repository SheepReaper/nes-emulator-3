using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace EmuSheep;

public sealed partial class MainPage : Page
{
    private readonly WriteableBitmap _frameBitmap = new(NesSystem.FrameWidth, NesSystem.FrameHeight);
    private readonly MainPageFramePresenter _presenter;
    private NesEmulationSession? _session;
    private string? _romFileName;
    private int _presentationQueued;
    private volatile bool _isUnloaded;

    public MainPage()
    {
        InitializeComponent();
        _presenter = new MainPageFramePresenter(_frameBitmap);
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
            var window = ((App)Application.Current).MainWindow;
            var file = await NesRomFileLoader.PickRomFileAsync(window);
            if (file == null || _isUnloaded)
            {
                return;
            }

            SetLoadingState(file.Name);
            var romData = await NesRomFileLoader.ReadStorageFileBytesAsync(file);
            await StartRomAsync(file.Name, romData);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
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
            var fileName = Path.GetFileName(romPath);
            SetLoadingState(fileName);
            var romData = await NesRomFileLoader.ReadRomFromPathAsync(romPath);
            await StartRomAsync(fileName, romData);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            OpenRomButton.IsEnabled = true;
            LoadingIndicator.IsActive = false;
        }
    }
}
