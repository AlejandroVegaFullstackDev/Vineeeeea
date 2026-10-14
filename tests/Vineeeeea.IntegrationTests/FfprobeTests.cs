using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Infrastructure.Implements;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.IntegrationTests;

public class FfprobeTests
{
    private static readonly AppConfig Config = AppConfig.FromEnvironment(Environment.GetEnvironmentVariable);
    private readonly FfprobeMediaProbe probe = new(new ProcessRunner(), Config);

    [Fact]
    public void Parser_reads_duration_streams_and_cover_art()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "codec_name": "mjpeg", "width": 600, "height": 600, "disposition": { "attached_pic": 1 } },
                { "codec_type": "audio", "codec_name": "mp3", "disposition": { "attached_pic": 0 } }
              ],
              "format": { "duration": "185.432000" }
            }
            """;

        var media = FfprobeJsonParser.Parse(json);

        Assert.Equal(185_432_000, media.DurationUs);
        Assert.True(media.Streams[0].IsAttachedPicture);
        Assert.Equal(ProbedStreamType.Audio, media.Streams[1].Type);
    }

    [Fact]
    public void Parser_rejects_unreadable_output()
    {
        Assert.Throws<ExternalServiceException>(() => FfprobeJsonParser.Parse("not json"));
    }

    [Fact]
    public async Task Probes_a_real_file_whose_path_has_spaces()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip con espacios.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);

        var media = await probe.ProbeAsync(mediaPath, CancellationToken.None);

        Assert.InRange(media.DurationUs ?? 0, 1_900_000, 2_100_000);
        Assert.Contains(media.Streams, stream => stream is { Type: ProbedStreamType.Video, Width: 320, Height: 240 });
        Assert.Contains(media.Streams, stream => stream.Type == ProbedStreamType.Audio);
    }

    [Fact]
    public async Task Reports_files_that_are_not_media()
    {
        using var directory = new TemporaryDirectory();
        var textPath = directory.Combine("notas.txt");
        await File.WriteAllTextAsync(textPath, "esto no es video");

        await Assert.ThrowsAsync<ExternalServiceException>(() => probe.ProbeAsync(textPath, CancellationToken.None));
    }

    [Fact]
    public async Task Reports_missing_files()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => probe.ProbeAsync(@"C:\no\existe.mp4", CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_stops_the_probe()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => probe.ProbeAsync(mediaPath, new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Reports_photos_as_single_image_formats()
    {
        using var directory = new TemporaryDirectory();
        var imagePath = directory.Combine("foto roja.png");
        await TestMedia.GenerateRedImageAsync(imagePath);

        var media = await probe.ProbeAsync(imagePath, CancellationToken.None);

        Assert.EndsWith("_pipe", media.FormatName);
        Assert.Contains(media.Streams, stream => stream is { Type: ProbedStreamType.Video, Width: 200, Height: 100 });
    }
}
