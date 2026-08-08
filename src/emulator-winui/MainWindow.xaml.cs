using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace EmuSheep;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow(string? startupRomPath = null)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        SizeAndCenterForCurrentDisplay();

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage), startupRomPath);
    }

    private void SizeAndCenterForCurrentDisplay()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is null)
            return;

        var workArea = displayArea.WorkArea;
        var bounds = InitialWindowLayout.Calculate(workArea.Width, workArea.Height);
        AppWindow.MoveAndResize(
            new RectInt32(
                workArea.X + bounds.X,
                workArea.Y + bounds.Y,
                bounds.Width,
                bounds.Height),
            displayArea);
    }
}
