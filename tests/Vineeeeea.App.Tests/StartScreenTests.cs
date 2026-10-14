using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vineeeeea.App.Views;
using Vineeeeea.Application.Utils;
using Vineeeeea.IntegrationTests;

namespace Vineeeeea.App.Tests;

public class StartScreenTests
{
    [AvaloniaFact]
    public void Without_a_project_the_start_screen_is_shown_and_the_editor_is_not()
    {
        using var harness = EditorHarness.Open();

        Assert.True(harness.Window.GetVisualDescendants().OfType<StartView>().Single().IsEffectivelyVisible);
        Assert.Contains(harness.ViewModel.Start.Shapes, shape => shape is { Shape: CanvasShape.Horizontal, IsSelected: true });
    }

    [AvaloniaFact]
    public void Choosing_vertical_and_starting_creates_a_1080_by_1920_project()
    {
        using var harness = EditorHarness.Open();
        var vertical = harness.ViewModel.Start.Shapes.Single(shape => shape.Shape == CanvasShape.Vertical);

        harness.ViewModel.Start.SelectShapeCommand.Execute(vertical);
        harness.ViewModel.Start.StartCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var settings = harness.Session.RequireProject().Settings;
        Assert.Equal((1080, 1920, 30, 48_000), (settings.Width, settings.Height, settings.FrameRate.Numerator, settings.AudioSampleRate));
        Assert.False(harness.Window.GetVisualDescendants().OfType<StartView>().Single().IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Dropping_a_vertical_video_on_the_start_screen_creates_a_vertical_project_with_it()
    {
        using var harness = EditorHarness.Open();
        var mediaPath = harness.Directory.Combine("vertical.mp4");
        await TestMedia.GenerateVerticalClipAsync(mediaPath);
        var data = await harness.FileDropAsync(mediaPath);
        var target = harness.CenterOf(harness.Window.GetVisualDescendants().OfType<StartView>().Single());

        harness.Window.DragDrop(target, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        harness.Window.DragDrop(target, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);

        await harness.WaitUntilAsync(() => harness.Session.Project?.Tracks.Any(track => track.Clips.Count > 0) == true);
        var settings = harness.Session.RequireProject().Settings;
        Assert.Equal((1080, 1920), (settings.Width, settings.Height));
    }

    [AvaloniaFact]
    [Trait("Category", "Screenshots")]
    public void Captures_the_start_screen()
    {
        var outputDirectory = Environment.GetEnvironmentVariable("VINEEEEEA_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        using var harness = EditorHarness.Open();
        Dispatcher.UIThread.RunJobs();
        using var frame = harness.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(outputDirectory, "20-inicio.png"), PngBitmapEncoderOptions.Default);
    }
}
