using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Vineeeeea.Domain.Utils;
using Vineeeeea.IntegrationTests;

namespace Vineeeeea.App.Tests;

/// <summary>
/// Renders the real window into PNG files for design reviews. It only writes when VINEEEEEA_SCREENSHOT_DIR is set,
/// so regular runs and CI stay side-effect free.
/// </summary>
public class ScreenshotTests
{
    private const string OutputDirectoryVariable = "VINEEEEEA_SCREENSHOT_DIR";

    [AvaloniaFact]
    [Trait("Category", "Screenshots")]
    public async Task Captures_the_main_editor_states()
    {
        var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryVariable);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        Directory.CreateDirectory(outputDirectory);
        using var harness = EditorHarness.Open();
        Save(harness, outputDirectory, "01-inicio-vacio.png");

        var clipPath = harness.Directory.Combine("paisaje.mp4");
        var photoPath = harness.Directory.Combine("foto.png");
        await TestMedia.GenerateClipAsync(clipPath);
        await TestMedia.GenerateRedImageAsync(photoPath);
        await harness.ViewModel.ImportFilesAndAppendAsync([clipPath, clipPath, photoPath]);
        var clips = harness.Session.RequireProject().Tracks.SelectMany(track => track.Clips).ToList();
        harness.ViewModel.ApplyFilter(clips[0].Id, "Vintage");
        harness.ViewModel.SelectClip(clips[1].Id);
        harness.ViewModel.Inspector.Parameters.First(row => row.EffectType == EffectCatalog.Dissolve).Value = 0.8;
        harness.ViewModel.Player.PlayheadPosition = 40;
        await harness.WaitUntilAsync(() => harness.ViewModel.Player.Preview is not null);
        await Task.Delay(1500);
        Save(harness, outputDirectory, "02-clip-de-video-con-efectos.png");

        harness.ViewModel.AddTextCommand.Execute(null);
        harness.ViewModel.Inspector.TextContent = "Mi primer video";
        harness.ViewModel.Inspector.TextHasBackground = true;
        await Task.Delay(1500);
        Save(harness, outputDirectory, "03-texto-seleccionado.png");
    }

    private static void Save(EditorHarness harness, string directory, string fileName)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = harness.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, fileName), PngBitmapEncoderOptions.Default);
    }
}
