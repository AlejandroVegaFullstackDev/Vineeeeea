using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Vineeeeea.App.Utils;
using Vineeeeea.App.ViewModels;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;
using Vineeeeea.Infrastructure.Implements;
using Vineeeeea.Infrastructure.Services;
using Vineeeeea.IntegrationTests;

namespace Vineeeeea.App.Tests;

/// <summary>
/// End-to-end runs on a real recording: import, edit through the easy panel and export, verifying the finished file
/// with ffprobe. They only run when VINEEEEEA_E2E_VIDEO points to a video, so the normal suite and CI stay untouched.
/// </summary>
public class EndToEndTests
{
    private const string VideoVariable = "VINEEEEEA_E2E_VIDEO";
    private const double KeptSeconds = 2;

    private static readonly IMediaProbe Probe =
        new FfprobeMediaProbe(new ProcessRunner(), AppConfig.FromEnvironment(Environment.GetEnvironmentVariable));

    [AvaloniaFact]
    [Trait("Category", "E2E")]
    public async Task A_real_video_starts_a_project_shaped_like_it()
    {
        if (RealVideo() is not { } video)
        {
            return;
        }

        using var harness = EditorHarness.Open();
        await harness.ViewModel.StartWithFilesAsync([video]);
        Dispatcher.UIThread.RunJobs();

        var project = harness.Session.RequireProject();
        var clip = Assert.Single(project.Tracks.SelectMany(track => track.Clips));
        Assert.Equal((1920, 1080), (project.Settings.Width, project.Settings.Height));
        Assert.Single(project.Assets);
        Assert.Equal(project.Assets[0].Id, clip.AssetId);
        Assert.True(clip.TimelineDurationFrames(project.Settings.FrameRate) > 1);

        await harness.SettleAsync();
    }

    [AvaloniaFact]
    [Trait("Category", "E2E")]
    public async Task Editing_and_exporting_a_real_video_produces_a_playable_file()
    {
        if (RealVideo() is not { } video)
        {
            return;
        }

        using var harness = EditorHarness.Open();
        await harness.ViewModel.StartWithFilesAsync([video]);
        var (clipId, keptFrames) = TrimToBriefClip(harness);
        harness.ViewModel.SelectClip(clipId);

        harness.ViewModel.ClipTools.ApplyMovementCommand.Execute(new MovementOption("Acercar", CameraMove.ZoomIn));
        harness.ViewModel.ApplyFilter(clipId, "Vintage");
        Dispatcher.UIThread.RunJobs();

        var clip = harness.Session.RequireProject().GetClip(clipId);
        Assert.True(clip.FindEffect(EffectCatalog.Transform)?.Keyframes.ContainsKey(EffectCatalog.Scale));
        Assert.Contains(clip.Effects, effect => effect.Type == EffectCatalog.Saturation);

        var exported = await ExportAsync(harness);
        var media = await Probe.ProbeAsync(exported, CancellationToken.None);
        var expectedSeconds = keptFrames / harness.Session.RequireProject().Settings.FrameRate.ToFramesPerSecond();

        Assert.Contains(media.Streams, stream => stream is { Type: ProbedStreamType.Video, Width: 1920, Height: 1080 });
        Assert.Contains(media.Streams, stream => stream.Type == ProbedStreamType.Audio);
        Assert.InRange((media.DurationUs ?? 0) / 1_000_000.0, expectedSeconds - 0.6, expectedSeconds + 0.6);

        await harness.SettleAsync();
    }

    [AvaloniaFact]
    [Trait("Category", "E2E")]
    public async Task A_reversed_real_video_still_exports_a_playable_file()
    {
        if (RealVideo() is not { } video)
        {
            return;
        }

        using var harness = EditorHarness.Open();
        await harness.ViewModel.StartWithFilesAsync([video]);
        var (clipId, _) = TrimToBriefClip(harness);
        harness.ViewModel.SelectClip(clipId);

        harness.ViewModel.ClipTools.ToggleReverseCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(harness.ViewModel.ClipTools.ReverseOn);

        var exported = await ExportAsync(harness);
        var media = await Probe.ProbeAsync(exported, CancellationToken.None);

        Assert.Contains(media.Streams, stream => stream is { Type: ProbedStreamType.Video, Width: 1920, Height: 1080 });
        Assert.Contains(media.Streams, stream => stream.Type == ProbedStreamType.Audio);

        await harness.SettleAsync();
    }

    private static string? RealVideo() =>
        Environment.GetEnvironmentVariable(VideoVariable) is { Length: > 0 } path && File.Exists(path) ? path : null;

    /// <summary>Keeps only the first couple of seconds so an export stays fast while still being a real encode.</summary>
    private static (Guid ClipId, long KeptFrames) TrimToBriefClip(EditorHarness harness)
    {
        var project = harness.Session.RequireProject();
        var frameRate = project.Settings.FrameRate;
        var clip = project.Tracks.SelectMany(track => track.Clips).First();
        var keptFrames = (long)Math.Round(KeptSeconds * frameRate.ToFramesPerSecond());
        var removeFrames = clip.TimelineDurationFrames(frameRate) - keptFrames;
        var deltaPixels = -(double)removeFrames / frameRate.ToFramesPerSecond() * TimelineScale.PixelsPerSecond;

        harness.ViewModel.TrimClipEnd(clip.Id, deltaPixels);
        Dispatcher.UIThread.RunJobs();

        var trimmed = harness.Session.RequireProject().GetClip(clip.Id);
        Assert.InRange(trimmed.TimelineDurationFrames(frameRate), keptFrames - 2, keptFrames + 2);
        return (clip.Id, keptFrames);
    }

    private static async Task<string> ExportAsync(EditorHarness harness)
    {
        var folder = harness.Directory.Combine("salida");
        Directory.CreateDirectory(folder);
        harness.ViewModel.ShowExportCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var export = harness.ViewModel.Export;
        export.FolderPath = folder;
        export.FileName = "e2e";
        export.SelectDestinationCommand.Execute(export.Destinations.Single(option => option.Destination.Id == ExportDestinations.YouTube));
        await export.ExportCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(export.HasFinished, harness.ViewModel.StatusMessage);
        var path = Path.Combine(folder, "e2e" + FileConventions.ExportExtension);
        Assert.True(File.Exists(path), $"No se creó el archivo exportado: {path}");
        return path;
    }
}
