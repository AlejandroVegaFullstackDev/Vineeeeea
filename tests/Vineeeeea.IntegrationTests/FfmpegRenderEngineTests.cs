using Vineeeeea.Application.Config;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;
using Vineeeeea.Infrastructure.Implements;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.IntegrationTests;

public class FfmpegRenderEngineTests
{
    private const long TwoSecondsUs = 2_000_000;

    private static readonly AppConfig Config = AppConfig.FromEnvironment(Environment.GetEnvironmentVariable);
    private static readonly FrameSize Canvas = new(320, 240);

    private readonly ProcessRunner processRunner = new();

    private static Project ComposedProject(string mediaPath)
    {
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, TwoSecondsUs, 320, 240, hasAudio: true);
        var main = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var overlay = new Track(Guid.NewGuid(), TrackKind.Video, "Video 2", isMuted: false, isHidden: false);

        var pictureInPicture = new Clip(Guid.NewGuid(), video.Id, 15, 0, TwoSecondsUs, 1,
        [
            EffectInstance.CreateDefault(EffectCatalog.Transform)
                .WithParameter(EffectCatalog.Scale, 0.4)
                .WithKeyframe(EffectCatalog.PositionX, 0, -0.5)
                .WithKeyframe(EffectCatalog.PositionX, TwoSecondsUs, 0.5),
            EffectInstance.CreateDefault(EffectCatalog.Opacity).WithParameter(EffectCatalog.Amount, 0.8),
            EffectInstance.CreateDefault(EffectCatalog.Brightness).WithKeyframe(EffectCatalog.Amount, 0, -0.5).WithKeyframe(EffectCatalog.Amount, 1_000_000, 0.2),
            EffectInstance.CreateDefault(EffectCatalog.Contrast).WithParameter(EffectCatalog.Amount, 1.3),
            EffectInstance.CreateDefault(EffectCatalog.Volume).WithKeyframe(EffectCatalog.Gain, 0, 0).WithKeyframe(EffectCatalog.Gain, 1_000_000, 1),
        ]);
        var slowMotion = new Clip(Guid.NewGuid(), video.Id, 0, 0, 1_000_000, 0.25, []);

        return new Project(Guid.NewGuid(), "Composición", new ProjectSettings(Canvas.Width, Canvas.Height, new FrameRate(30, 1), 48_000))
            .AddAsset(video)
            .AddTrack(main)
            .AddTrack(overlay)
            .InsertClip(main.Id, slowMotion)
            .InsertClip(overlay.Id, pictureInPicture);
    }

    [Fact]
    public async Task Renders_a_composed_still_at_the_requested_size()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip con espacios.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var plan = RenderPlanBuilder.Build(ComposedProject(mediaPath), 20, 21, Canvas);
        var frames = new List<byte[]>();

        await new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).StreamVideoAsync(plan, (pixels, _) =>
        {
            frames.Add(pixels);
            return ValueTask.CompletedTask;
        }, CancellationToken.None);

        var frame = Assert.Single(frames);
        Assert.Equal(Canvas.FrameBytes, frame.Length);
        Assert.Contains(frame, value => value is not 0 and not 255);
    }

    [Fact]
    public async Task A_rotated_clip_renders_through_the_transpose_filter()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("de lado.mp4");
        await TestMedia.GenerateVerticalClipAsync(mediaPath);
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, 1_000_000, 180, 320, hasAudio: false);
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var clip = new Clip(Guid.NewGuid(), video.Id, 0, 0, 1_000_000, 1, []);
        var project = new Project(Guid.NewGuid(), "Girado", new ProjectSettings(Canvas.Width, Canvas.Height, new FrameRate(30, 1), 48_000))
            .AddAsset(video)
            .AddTrack(track)
            .InsertClip(track.Id, clip)
            .RotateClipQuarterTurn(clip.Id);

        byte[]? frame = null;
        await new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).StreamVideoAsync(
            RenderPlanBuilder.Build(project, 0, 1, Canvas),
            (pixels, _) =>
            {
                frame = pixels;
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.NotNull(frame);
        Assert.Equal(Canvas.FrameBytes, frame!.Length);
        Assert.Contains(frame, value => value is not 0 and not 255);
    }

    [Fact]
    public async Task The_blurred_background_fill_fills_the_side_bars_with_content()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("vertical.mp4");
        await TestMedia.GenerateVerticalClipAsync(mediaPath);
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, 1_000_000, 180, 320, hasAudio: false);
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var project = new Project(Guid.NewGuid(), "Fondo", new ProjectSettings(Canvas.Width, Canvas.Height, new FrameRate(30, 1), 48_000))
            .AddAsset(video)
            .AddTrack(track)
            .InsertClip(track.Id, new Clip(Guid.NewGuid(), video.Id, 0, 0, 1_000_000, 1,
                [EffectInstance.CreateDefault(EffectCatalog.Fill)]));

        byte[]? frame = null;
        await new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).StreamVideoAsync(
            RenderPlanBuilder.Build(project, 0, 1, Canvas), (pixels, _) => { frame = pixels; return ValueTask.CompletedTask; }, CancellationToken.None);

        Assert.NotNull(frame);
        // A vertical clip leaves side bars on a 320x240 canvas; with the fill they hold blurred content, not black.
        // Without the fill the whole left-bar column (x=2) is black; with it, some pixel in that column has colour.
        var leftBarHasContent = Enumerable.Range(0, Canvas.Height)
            .Select(y => (y * Canvas.Width + 2) * 4)
            .Any(index => frame![index] != 0 || frame[index + 1] != 0 || frame[index + 2] != 0);
        Assert.True(leftBarHasContent, "La franja izquierda quedó negra: el fondo no se rellenó.");
    }

    [Fact]
    public async Task Gaps_render_as_black()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, TwoSecondsUs, 320, 240, hasAudio: true);
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var project = new Project(Guid.NewGuid(), "Hueco", new ProjectSettings(320, 240, new FrameRate(30, 1), 48_000))
            .AddAsset(video)
            .AddTrack(track)
            .InsertClip(track.Id, new Clip(Guid.NewGuid(), video.Id, 30, 0, TwoSecondsUs, 1, []));
        byte[]? frame = null;

        await new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).StreamVideoAsync(
            RenderPlanBuilder.Build(project, 5, 6, Canvas),
            (pixels, _) =>
            {
                frame = pixels;
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.NotNull(frame);
        Assert.All(frame.Where((_, index) => index % 4 != 3), value => Assert.True(value < 20));
    }

    [Fact]
    public async Task Exports_a_playable_mp4_with_video_and_audio()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip.mp4");
        var outputPath = directory.Combine("export final.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var project = ComposedProject(mediaPath);
        var plan = RenderPlanBuilder.Build(project, 0, project.EndFrame(), Canvas);
        var reported = new List<double>();

        await new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).ExportAsync(plan, outputPath, Config.ExportEncoding, new SynchronousProgress(reported.Add), CancellationToken.None);

        var probed = await new FfprobeMediaProbe(processRunner, Config).ProbeAsync(outputPath, CancellationToken.None);
        Assert.InRange(probed.DurationUs ?? 0, 3_950_000, 4_100_000);
        Assert.Contains(probed.Streams, stream => stream is { Type: ProbedStreamType.Video, Width: 320, Height: 240 });
        Assert.Contains(probed.Streams, stream => stream.Type == ProbedStreamType.Audio);
        Assert.Equal(1, reported[^1]);
        Assert.False(File.Exists(outputPath + ".part"));
    }

    [Fact]
    public async Task Exports_a_horizontal_project_to_the_WhatsApp_destination_at_1280x720()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip.mp4");
        var outputPath = directory.Combine("whatsapp.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, TwoSecondsUs, 320, 240, hasAudio: true);
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var session = new EditorSession();
        session.Open(new Project(Guid.NewGuid(), "Demo", new ProjectSettings(1920, 1080, new FrameRate(30, 1), 48_000))
            .AddAsset(video)
            .AddTrack(track)
            .InsertClip(track.Id, new Clip(Guid.NewGuid(), video.Id, 0, 0, TwoSecondsUs, 1, [])), filePath: null);
        var controller = new ExportController(session, new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config));

        await controller.ExportAsync(ExportDestinations.Get(ExportDestinations.WhatsApp), outputPath, new SynchronousProgress(_ => { }), CancellationToken.None);

        var probed = await new FfprobeMediaProbe(processRunner, Config).ProbeAsync(outputPath, CancellationToken.None);
        Assert.Contains(probed.Streams, stream => stream is { Type: ProbedStreamType.Video, Width: 1280, Height: 720 });
    }

    [Fact]
    public async Task Exports_a_clip_with_the_youtube_effects_flip_chromakey_denoise_and_normalize()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip.mp4");
        var outputPath = directory.Combine("efectos.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, TwoSecondsUs, 320, 240, hasAudio: true);
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var clip = new Clip(Guid.NewGuid(), video.Id, 0, 0, TwoSecondsUs, 1,
        [
            EffectInstance.CreateDefault(EffectCatalog.Flip).WithParameter(EffectCatalog.Horizontal, 1),
            EffectInstance.CreateDefault(EffectCatalog.ChromaKey),
            EffectInstance.CreateDefault(EffectCatalog.Denoise).WithParameter(EffectCatalog.Amount, 0.6),
            EffectInstance.CreateDefault(EffectCatalog.Normalize),
        ]);
        var project = new Project(Guid.NewGuid(), "Efectos", new ProjectSettings(Canvas.Width, Canvas.Height, new FrameRate(30, 1), 48_000))
            .AddAsset(video)
            .AddTrack(track)
            .InsertClip(track.Id, clip);

        await new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).ExportAsync(
            RenderPlanBuilder.Build(project, 0, project.EndFrame(), Canvas), outputPath, Config.ExportEncoding,
            new SynchronousProgress(_ => { }), CancellationToken.None);

        var probed = await new FfprobeMediaProbe(processRunner, Config).ProbeAsync(outputPath, CancellationToken.None);
        Assert.InRange(probed.DurationUs ?? 0, 1_900_000, 2_100_000);
        Assert.Contains(probed.Streams, stream => stream.Type == ProbedStreamType.Audio);
    }

    [Fact]
    public async Task A_cancelled_export_leaves_no_files_behind()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip.mp4");
        var outputPath = directory.Combine("cancelado.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var project = ComposedProject(mediaPath);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).ExportAsync(
            RenderPlanBuilder.Build(project, 0, project.EndFrame(), Canvas),
            outputPath,
            Config.ExportEncoding,
            new SynchronousProgress(_ => { }),
            new CancellationToken(canceled: true)));

        Assert.False(File.Exists(outputPath));
        Assert.False(File.Exists(outputPath + ".part"));
    }

    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    [Fact]
    public async Task Renders_a_photo_clip_well_past_the_start_of_the_image()
    {
        using var directory = new TemporaryDirectory();
        var imagePath = directory.Combine("foto.png");
        await TestMedia.GenerateRedImageAsync(imagePath);
        var photo = MediaAsset.StillImage(Guid.NewGuid(), imagePath, 200, 100);
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var project = new Project(Guid.NewGuid(), "Foto", new ProjectSettings(320, 240, new FrameRate(30, 1), 48_000))
            .AddAsset(photo)
            .AddTrack(track)
            .InsertClip(track.Id, new Clip(Guid.NewGuid(), photo.Id, 0, 0, photo.DefaultClipDurationUs, 1, []));
        byte[]? frame = null;

        await new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).StreamVideoAsync(
            RenderPlanBuilder.Build(project, 120, 121, Canvas),
            (pixels, _) =>
            {
                frame = pixels;
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        var center = (Canvas.Height / 2 * Canvas.Width + Canvas.Width / 2) * VideoFrame.BytesPerPixel;
        Assert.NotNull(frame);
        var (blue, green, red) = (frame[center], frame[center + 1], frame[center + 2]);
        Assert.True(red > 200 && green < 60 && blue < 60, $"El centro debería ser rojo y es B={blue} G={green} R={red}.");
    }

    [Fact]
    public async Task Exports_every_effect_filter_fade_and_dissolve_together()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip.mp4");
        var outputPath = directory.Combine("todo.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, TwoSecondsUs, 320, 240, hasAudio: true);
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var first = new Clip(Guid.NewGuid(), video.Id, 0, 0, 1_000_000, 1.5,
        [
            EffectInstance.CreateDefault(EffectCatalog.Crop).WithParameter(EffectCatalog.Left, 0.1).WithParameter(EffectCatalog.Bottom, 0.2),
            EffectInstance.CreateDefault(EffectCatalog.Fade).WithParameter(EffectCatalog.InSeconds, 0.3),
            EffectInstance.CreateDefault(EffectCatalog.AudioFade).WithParameter(EffectCatalog.InSeconds, 0.3).WithParameter(EffectCatalog.OutSeconds, 0.2),
        ]);
        var second = new Clip(Guid.NewGuid(), video.Id, 20, 1_000_000, 1_000_000, 1,
        [
            EffectInstance.CreateDefault(EffectCatalog.Dissolve).WithParameter(EffectCatalog.Seconds, 0.4),
            EffectInstance.CreateDefault(EffectCatalog.Fade).WithParameter(EffectCatalog.OutSeconds, 0.5),
            EffectInstance.CreateDefault(EffectCatalog.Opacity).WithParameter(EffectCatalog.Amount, 0.7),
        ]);
        var project = new Project(Guid.NewGuid(), "Todo", new ProjectSettings(320, 240, new FrameRate(30, 1), 48_000))
            .AddAsset(video)
            .AddTrack(track)
            .InsertClip(track.Id, first)
            .InsertClip(track.Id, second);
        foreach (var preset in FilterPresets.All)
        {
            project = project.ApplyFilter(second.Id, preset);
        }

        project = project
            .ApplyFilter(first.Id, FilterPresets.Get("Vintage"))
            .SetClipEffect(first.Id, EffectInstance.CreateDefault(EffectCatalog.Blur).WithParameter(EffectCatalog.Amount, 2))
            .SetClipEffect(first.Id, EffectInstance.CreateDefault(EffectCatalog.Sharpen).WithParameter(EffectCatalog.Amount, 1));

        await new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).ExportAsync(
            RenderPlanBuilder.Build(project, 0, project.EndFrame(), Canvas), outputPath, Config.ExportEncoding, new SynchronousProgress(_ => { }), CancellationToken.None);

        var probed = await new FfprobeMediaProbe(processRunner, Config).ProbeAsync(outputPath, CancellationToken.None);
        Assert.InRange(probed.DurationUs ?? 0, 1_500_000, 1_800_000);
    }

    [Fact]
    public async Task A_reversed_clip_plays_its_last_frame_first()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("rojo-azul.mp4");
        await TestMedia.GenerateRedThenBlueClipAsync(mediaPath);
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, TwoSecondsUs, 320, 240, hasAudio: false);
        var forward = new Clip(Guid.NewGuid(), video.Id, 0, 0, TwoSecondsUs, 1, []);
        var reversed = new Clip(Guid.NewGuid(), video.Id, 0, 0, TwoSecondsUs, 1,
            [EffectInstance.CreateDefault(EffectCatalog.Reverse).WithParameter(EffectCatalog.Amount, 1)]);

        var forwardFrames = await RenderFramesAsync(ProjectWithClip(video, forward));
        var reversedFrames = await RenderFramesAsync(ProjectWithClip(video, reversed));

        Assert.True(IsRed(forwardFrames[0], Canvas.Width / 2, Canvas.Height / 2), "El clip normal empieza en rojo.");
        Assert.True(IsBlue(reversedFrames[0], Canvas.Width / 2, Canvas.Height / 2), "El clip al revés empieza por su último fotograma.");
        Assert.True(IsBlue(forwardFrames[^1], Canvas.Width / 2, Canvas.Height / 2), "El clip normal termina en azul.");
        Assert.True(IsRed(reversedFrames[^1], Canvas.Width / 2, Canvas.Height / 2), "El clip al revés termina en rojo.");
    }

    [Fact]
    public async Task An_animated_scale_zooms_the_picture_over_time()
    {
        using var directory = new TemporaryDirectory();
        var imagePath = directory.Combine("cuadro.png");
        await TestMedia.GenerateCornerSquareImageAsync(imagePath);
        var photo = MediaAsset.StillImage(Guid.NewGuid(), imagePath, 320, 240);
        var transform = EffectInstance.CreateDefault(EffectCatalog.Transform)
            .WithKeyframe(EffectCatalog.Scale, 0, 1)
            .WithKeyframe(EffectCatalog.Scale, TwoSecondsUs, 1.35);
        var clip = new Clip(Guid.NewGuid(), photo.Id, 0, 0, TwoSecondsUs, 1, [transform]);

        var frames = await RenderFramesAsync(ProjectWithClip(photo, clip));

        Assert.Equal(60, frames.Count);
        Assert.True(IsRed(frames[0], 30, 30), "El cuadro rojo debería verse al empezar.");
        Assert.False(IsRed(frames[^1], 30, 30), "Al acercar, el cuadro rojo debería salir del encuadre.");
    }

    private static Project ProjectWithClip(MediaAsset asset, Clip clip)
    {
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        return new Project(Guid.NewGuid(), "Prueba", new ProjectSettings(Canvas.Width, Canvas.Height, new FrameRate(30, 1), 48_000))
            .AddAsset(asset)
            .AddTrack(track)
            .InsertClip(track.Id, clip);
    }

    private async Task<List<byte[]>> RenderFramesAsync(Project project)
    {
        var frames = new List<byte[]>();
        await new FfmpegRenderEngine(processRunner, new TextFileStore(Config), Config).StreamVideoAsync(
            RenderPlanBuilder.Build(project, 0, project.EndFrame(), Canvas),
            (data, _) =>
            {
                frames.Add(data);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);
        return frames;
    }

    private static bool IsRed(byte[] frame, int x, int y) => Channel(frame, x, y) is var (blue, green, red) && red > 180 && green < 80 && blue < 80;

    private static bool IsBlue(byte[] frame, int x, int y) => Channel(frame, x, y) is var (blue, green, red) && blue > 180 && red < 80 && green < 80;

    private static (byte Blue, byte Green, byte Red) Channel(byte[] frame, int x, int y)
    {
        var index = (y * Canvas.Width + x) * VideoFrame.BytesPerPixel;
        return (frame[index], frame[index + 1], frame[index + 2]);
    }
}
