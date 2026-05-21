using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using shmtu.captcha.onnx.gui.ViewModels;
using shmtu.captcha.onnx.gui.Views;

namespace shmtu.captcha.onnx.gui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
