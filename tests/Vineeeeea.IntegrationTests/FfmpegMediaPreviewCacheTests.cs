using Vineeeeea.Application.Config;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Infrastructure.Implements;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.IntegrationTests;

public class FfmpegMediaPreviewCacheTests
{
    [Fact]
    public async Task Creates_thumbnails_and_waveforms_once_and_reuses_them()
    {
        using var directory = new TemporaryDirectory();
        var mediaPath = directory.Combine("clip.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var config = AppConfig.FromEnvironment(Environment.GetEnvironmentVariable) with { CacheDirectory = directory.Combine("cache") };
        var cache = new FfmpegMediaPreviewCache(new ProcessRunner(), config);
        var asset = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, 2_000_000, 320, 240, hasAudio: true);

        var thumbnail = await cache.GetThumbnailAsync(asset, CancellationToken.None);
        var waveform = await cache.GetWaveformAsync(asset, CancellationToken.None);
        var firstWrite = File.GetLastWriteTimeUtc(thumbnail);

        Assert.True(new FileInfo(thumbnail).Length > 0);
        Assert.True(new FileInfo(waveform).Length > 0);
        Assert.Equal(thumbnail, await cache.GetThumbnailAsync(asset, CancellationToken.None));
        Assert.Equal(firstWrite, File.GetLastWriteTimeUtc(thumbnail));
        Assert.Empty(Directory.GetFiles(config.CacheDirectory, "*.part"));
    }

    [Fact]
    public async Task Reports_missing_media()
    {
        using var directory = new TemporaryDirectory();
        var config = AppConfig.FromEnvironment(Environment.GetEnvironmentVariable) with { CacheDirectory = directory.Path };
        var asset = new MediaAsset(Guid.NewGuid(), MediaKind.Video, @"C:\no\existe.mp4", 2_000_000, 320, 240, hasAudio: true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new FfmpegMediaPreviewCache(new ProcessRunner(), config).GetThumbnailAsync(asset, CancellationToken.None));
    }
}
