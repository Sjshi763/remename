using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using remename.Diagnostics;
using remename.ViewModels;
using remename.Views;
using System;
using System.Threading.Tasks;

namespace remename;

public partial class App : Application
{
    public override void Initialize()
    {
        AppLogger.Initialize();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLogger.Error("Unhandled application exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            AppLogger.Error("Unobserved task exception", e.Exception);

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var mainView = PlatformHelper.IsMobile
                ? (Avalonia.Controls.Control)new MobileMainView()
                : new MainView();

            mainView.DataContext = new MainViewModel();
            singleViewPlatform.MainView = mainView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
