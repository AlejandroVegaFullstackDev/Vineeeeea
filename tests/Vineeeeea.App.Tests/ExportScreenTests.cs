using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vineeeeea.App.Views;
using Vineeeeea.Application.Utils;

namespace Vineeeeea.App.Tests;

public class ExportScreenTests
{
    private static void StartProject(EditorHarness harness)
    {
        harness.ViewModel.Start.StartCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Opening_export_shows_the_four_destinations_and_hides_the_editor()
    {
        using var harness = EditorHarness.Open();
        StartProject(harness);

        harness.ViewModel.ShowExportCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(harness.ViewModel.IsExportScreen);
        Assert.True(harness.Window.GetVisualDescendants().OfType<ExportView>().Single().IsEffectivelyVisible);
        Assert.Equal(4, harness.ViewModel.Export.Destinations.Count);
    }

    [AvaloniaFact]
    public void A_horizontal_project_warns_about_bars_only_for_a_vertical_destination()
    {
        using var harness = EditorHarness.Open();
        StartProject(harness);
        harness.ViewModel.ShowExportCommand.Execute(null);
        var export = harness.ViewModel.Export;

        var tikTok = export.Destinations.Single(option => option.Destination.Id == ExportDestinations.TikTok);
        export.SelectDestinationCommand.Execute(tikTok);
        Assert.False(string.IsNullOrEmpty(export.LetterboxNotice));

        var youTube = export.Destinations.Single(option => option.Destination.Id == ExportDestinations.YouTube);
        export.SelectDestinationCommand.Execute(youTube);
        Assert.True(string.IsNullOrEmpty(export.LetterboxNotice));
    }

    [AvaloniaFact]
    public void Leaving_export_returns_to_the_editor()
    {
        using var harness = EditorHarness.Open();
        StartProject(harness);
        harness.ViewModel.ShowExportCommand.Execute(null);

        harness.ViewModel.Export.BackCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(harness.ViewModel.IsEasyEditing);
        Assert.False(harness.ViewModel.IsExportScreen);
    }

    [AvaloniaFact]
    [Trait("Category", "Screenshots")]
    public void Captures_the_export_screen()
    {
        var outputDirectory = Environment.GetEnvironmentVariable("VINEEEEEA_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        using var harness = EditorHarness.Open();
        StartProject(harness);
        harness.ViewModel.ShowExportCommand.Execute(null);
        var tikTok = harness.ViewModel.Export.Destinations.Single(option => option.Destination.Id == ExportDestinations.TikTok);
        harness.ViewModel.Export.SelectDestinationCommand.Execute(tikTok);
        Dispatcher.UIThread.RunJobs();

        using var frame = harness.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(outputDirectory, "30-exportar.png"), PngBitmapEncoderOptions.Default);
    }
}
