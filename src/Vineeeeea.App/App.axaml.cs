using System;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Vineeeeea.App.Views;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Services;

namespace Vineeeeea.App;

public partial class App : Avalonia.Application
{
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

        base.OnFrameworkInitializationCompleted();
    }
}
