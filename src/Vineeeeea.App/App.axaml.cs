using System;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Vineeeeea.App.Views;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Services;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.App;

public partial class App : Avalonia.Application
{
    /// <summary>Set by a mobile head before the app starts: iOS and Android cannot launch ffmpeg as a process.</summary>
    public static IToolRunner? MobileToolRunner { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var viewModel = CompositionRoot.CreateMainViewModel(
                window, AppConfig.FromEnvironment(Environment.GetEnvironmentVariable), new EditorSession());
            window.DataContext = viewModel;
            desktop.MainWindow = window;

            if (desktop.Args?.FirstOrDefault() is { } projectPath)
            {
                _ = viewModel.OpenProjectFromPathAsync(projectPath);
            }
        }

        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MobileShell(MobileToolRunner);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
