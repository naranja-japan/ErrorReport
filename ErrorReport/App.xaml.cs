using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace Naranja.ErrorReport;

public partial class App : Application
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appID);

    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SetCurrentProcessExplicitAppUserModelID("Naranja.ErrorReport");

        _window = new MainWindow();
        _window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "favicon.ico"));
        _window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = false;
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(520, 680));

        var presenter = _window.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        _window.Activate();
    }
}
