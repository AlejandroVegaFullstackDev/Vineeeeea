using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Vineeeeea.IntegrationTests;

namespace Vineeeeea.App.Tests;

public class AdvancedEditorTests
{
    private static async Task<Guid> StartWithClipInAdvancedAsync(EditorHarness harness)
    {
        var clipPath = harness.Directory.Combine("paisaje.mp4");
        await TestMedia.GenerateClipAsync(clipPath);
        await harness.ViewModel.ImportFilesAndAppendAsync([clipPath]);
        var clip = harness.Session.RequireProject().Tracks.SelectMany(track => track.Clips).Single();
        harness.ViewModel.SelectClip(clip.Id);
        harness.ViewModel.ToggleAdvancedModeCommand.Execute(null);
        await harness.SettleAsync();
        return clip.Id;
    }

    [AvaloniaFact]
    public async Task Stepping_one_frame_advances_the_timecode()
    {
        using var harness = EditorHarness.Open();
        await StartWithClipInAdvancedAsync(harness);
        harness.ViewModel.Player.PlayheadPosition = 0;

        harness.ViewModel.Player.StepForwardCommand.Execute(null);

        Assert.Equal(1, harness.ViewModel.Player.PlayheadFrame);
        Assert.Equal("00:00:00:01", harness.ViewModel.Player.Timecode);
    }

    [AvaloniaFact]
    public async Task Locking_a_track_blocks_edits_to_its_clips()
    {
        using var harness = EditorHarness.Open();
        var clipId = await StartWithClipInAdvancedAsync(harness);
        var track = harness.ViewModel.Tracks.Single();

        harness.ViewModel.ToggleTrackLocked(track);
        Assert.True(harness.Session.RequireProject().Tracks.Single().IsLocked);

        harness.ViewModel.Player.PlayheadPosition = 15;
        harness.ViewModel.SplitSelectedClipCommand.Execute(null);

        Assert.Single(harness.Session.RequireProject().Tracks.Single().Clips);
        Assert.Contains("bloqueada", harness.ViewModel.StatusMessage);
        await harness.SettleAsync();
    }

    [AvaloniaFact]
    public async Task The_magnet_can_be_turned_off()
    {
        using var harness = EditorHarness.Open();
        await StartWithClipInAdvancedAsync(harness);

        Assert.True(harness.ViewModel.MagnetEnabled);
        harness.ViewModel.ToggleMagnetCommand.Execute(null);
        Assert.False(harness.ViewModel.MagnetEnabled);
    }

    [AvaloniaFact]
    [Trait("Category", "Screenshots")]
    public async Task Captures_the_advanced_editor()
    {
        var outputDirectory = Environment.GetEnvironmentVariable("VINEEEEEA_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        using var harness = EditorHarness.Open();
        await StartWithClipInAdvancedAsync(harness);
        harness.ViewModel.Player.PlayheadPosition = 15;
        await harness.WaitUntilAsync(() => harness.ViewModel.Player.Preview is not null);
        await Task.Delay(1200);

        using var frame = harness.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(outputDirectory, "50-editor-avanzado.png"), PngBitmapEncoderOptions.Default);
    }
}
