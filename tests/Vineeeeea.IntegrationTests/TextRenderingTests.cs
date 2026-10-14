using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Models;
using Vineeeeea.Infrastructure.Implements;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.IntegrationTests;

public class TextRenderingTests
{
    private static readonly FrameSize Canvas = new(320, 240);

    [Fact]
    public async Task Draws_text_with_tricky_characters_over_the_layer_below_without_hiding_it()
    {
        using var directory = new TemporaryDirectory();
        var imagePath = directory.Combine("fondo rojo.png");
        await TestMedia.GenerateRedImageAsync(imagePath);
        var config = AppConfig.FromEnvironment(Environment.GetEnvironmentVariable) with { CacheDirectory = directory.Combine("caché con espacios") };
        var background = MediaAsset.StillImage(Guid.NewGuid(), imagePath, 200, 100);
        var title = MediaAsset.CreateText(Guid.NewGuid(), new TextStyle("Hola: 'mundo' 100% [ok]", 0.15, "#FFFFFF", hasBackground: true, isBold: true));
        var videoTrack = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var textTrack = new Track(Guid.NewGuid(), TrackKind.Text, "Texto 1", isMuted: false, isHidden: false);
        var project = new Project(Guid.NewGuid(), "Títulos", new ProjectSettings(Canvas.Width, Canvas.Height, new FrameRate(30, 1), 48_000))
            .AddAsset(background)
            .AddAsset(title)
            .AddTrack(videoTrack)
            .AddTrack(textTrack)
            .InsertClip(videoTrack.Id, new Clip(Guid.NewGuid(), background.Id, 0, 0, 2_000_000, 1, []))
            .InsertClip(textTrack.Id, new Clip(Guid.NewGuid(), title.Id, 0, 0, 2_000_000, 1, []));
        byte[]? frame = null;

        await new FfmpegRenderEngine(new ProcessRunner(), new TextFileStore(config), config).StreamVideoAsync(
            RenderPlanBuilder.Build(project, 10, 11, Canvas),
            (pixels, _) =>
            {
                frame = pixels;
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.NotNull(frame);
        var besideText = PixelAt(frame, 5, 55);
        Assert.True(besideText.Red > 200 && besideText.Green < 60, $"Fuera del texto debería verse la foto roja y es {besideText}.");
        var rows = Enumerable.Range(Canvas.Height / 2 - 20, 40);
        var columns = Enumerable.Range(20, Canvas.Width - 40);
        Assert.Contains(rows.SelectMany(y => columns.Select(x => PixelAt(frame, x, y))), pixel => pixel is { Red: > 220, Green: > 220, Blue: > 220 });
    }

    private static (byte Blue, byte Green, byte Red) PixelAt(byte[] frame, int x, int y)
    {
        var offset = (y * Canvas.Width + x) * VideoFrame.BytesPerPixel;
        return (frame[offset], frame[offset + 1], frame[offset + 2]);
    }
}
