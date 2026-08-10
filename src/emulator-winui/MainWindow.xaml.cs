using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SR.Emulation.Nes;
using Windows.Graphics;
using VirtualKey = Windows.System.VirtualKey;

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
    private readonly KeyboardControllerState _keyboard = new();

    public MainWindow(string? startupRomPath = null)
    {
        InitializeComponent();

        RootLayout.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootLayout_KeyDown), true);
        RootLayout.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(RootLayout_KeyUp), true);
        Activated += MainWindow_Activated;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        SizeAndCenterForCurrentDisplay();

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage), startupRomPath);
    }

    internal event Action<NesControllerButton>? ControllerStateChanged;
    internal NesControllerButton ControllerButtons => _keyboard.Buttons;

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e) => UpdateControllerKey(e, true);
    private void RootLayout_KeyUp(object sender, KeyRoutedEventArgs e) => UpdateControllerKey(e, false);

    private void UpdateControllerKey(KeyRoutedEventArgs e, bool pressed)
    {
        var key = e.Key switch
        {
            VirtualKey.Z => ControllerKey.A,
            VirtualKey.X => ControllerKey.B,
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => ControllerKey.Select,
            VirtualKey.Enter => ControllerKey.Start,
            VirtualKey.Up => ControllerKey.Up,
            VirtualKey.Down => ControllerKey.Down,
            VirtualKey.Left => ControllerKey.Left,
            VirtualKey.Right => ControllerKey.Right,
            _ => (ControllerKey?)null
        };
        if (!key.HasValue) return;

        e.Handled = true;
        if (_keyboard.SetPressed(key.Value, pressed))
            ControllerStateChanged?.Invoke(_keyboard.Buttons);
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState != WindowActivationState.Deactivated || !_keyboard.Clear()) return;
        ControllerStateChanged?.Invoke(NesControllerButton.None);
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
