using System.Windows;

namespace FlyVideos;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        window.Initialize(e.Args);
        window.Show();
    }
}
