using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Vineeeeea.App.ViewModels;
using Vineeeeea.App.Views;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Services;
using Vineeeeea.IntegrationTests;

namespace Vineeeeea.App.Tests;

internal sealed class EditorHarness : IDisposable
{
    private const int WaitStepMilliseconds = 50;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(20);

    private EditorHarness(MainWindow window, MainViewModel viewModel, EditorSession session, TemporaryDirectory directory)
    {
        Window = window;
        ViewModel = viewModel;
        Session = session;
        Directory = directory;
    }

    public MainWindow Window { get; }
    public MainViewModel ViewModel { get; }
    public EditorSession Session { get; }
    public TemporaryDirectory Directory { get; }

    public static EditorHarness Open()
    {
        var directory = new TemporaryDirectory();
        var config = AppConfig.FromEnvironment(Environment.GetEnvironmentVariable) with { CacheDirectory = directory.Combine("cache"), RecentProjectsFile = directory.Combine("recientes.json"), PreferencesFile = directory.Combine("preferencias.json") };
        var session = new EditorSession();
        var window = new MainWindow { Width = 1400, Height = 860 };
        var viewModel = CompositionRoot.CreateMainViewModel(window, config, session);
        window.DataContext = viewModel;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new EditorHarness(window, viewModel, session, directory);
    }

    public async Task<DataTransfer> FileDropAsync(string filePath)
    {
        var file = await Window.StorageProvider.TryGetFileFromPathAsync(filePath);
        Assert.NotNull(file);
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateFile(file));
        return data;
    }

    public Point CenterOf(string controlName) => CenterOf(Window.FindControl<Control>(controlName)!);

    public Point CenterOf(Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)!.Value;

    public async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + MaxWait;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "La condición no se cumplió a tiempo.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(WaitStepMilliseconds);
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Lets any in-flight preview render finish so its ffmpeg process releases the media file before teardown.</summary>
    public async Task SettleAsync()
    {
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(1500);
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose()
    {
        Window.Close();
        Directory.Dispose();
    }
}
