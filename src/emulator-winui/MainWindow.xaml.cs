using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

using Windows.Graphics;


using VirtualKey = Windows.System.VirtualKey;

namespace EmuSheep;
public sealed partial class MainWindow : Window
{
    private readonly KeyboardControllerState _keyboard = new();
    private bool _controllerInputEnabled;

    public MainWindow(string? startupRomPath = null)
    {
        InitializeComponent();

        RootLayout.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(RootLayout_KeyDown), true);
        RootLayout.AddHandler(UIElement.PreviewKeyUpEvent, new KeyEventHandler(RootLayout_KeyUp), true);
        Activated += MainWindow_Activated;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        SizeAndCenterForCurrentDisplay();

        RootFrame.Navigate(typeof(MainPage), startupRomPath);
    }

    internal event Action<NesControllerButton>? ControllerStateChanged;
    internal NesControllerButton ControllerButtons => _keyboard.Buttons;
    internal bool ControllerInputEnabled
    {
        get => _controllerInputEnabled;
        set
        {
            if (_controllerInputEnabled == value) return;
            _controllerInputEnabled = value;
            if (!value && _keyboard.Clear())
                ControllerStateChanged?.Invoke(NesControllerButton.None);
        }
    }

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e) => UpdateControllerKey(e, true);
    private void RootLayout_KeyUp(object sender, KeyRoutedEventArgs e) => UpdateControllerKey(e, false);

    private void UpdateControllerKey(KeyRoutedEventArgs e, bool pressed)
    {
        if (!ControllerInputEnabled) return;

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
