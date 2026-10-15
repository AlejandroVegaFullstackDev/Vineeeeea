using System.Security.Cryptography;
using System.Text;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Infrastructure.Services;
using Vineeeeea.Infrastructure.Utils;
using static Vineeeeea.Infrastructure.Utils.FfmpegExpressions;

namespace Vineeeeea.Infrastructure.Implements;

/// <summary>
/// Thumbnails and waveforms are derived data: they live outside the project, are keyed by the media file's identity
/// (path, size, modification time) and can be deleted at any time to be regenerated.
/// </summary>
public sealed class FfmpegMediaPreviewCache(IToolRunner processRunner, AppConfig config) : IMediaPreviewCache
{
    private const int ThumbnailHeight = 72;
    private const long MaxThumbnailOffsetUs = 1_000_000;
    private const string WaveformSize = "1200x64";
    private const int CacheKeyLength = 32;

    public Task<string> GetThumbnailAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        var offsetUs = asset.Kind == MediaKind.Image ? 0 : Math.Min(MaxThumbnailOffsetUs, asset.DurationUs / 2);
        return GetOrCreateAsync(asset, "thumb", outputPath =>
        [
            "-v", "error", "-nostdin", "-y",
            "-ss", Seconds(offsetUs),
            "-i", FfmpegConventions.LocalFileProtocol + asset.Path,
            "-frames:v", "1",
            "-vf", string.Concat("scale=-2:", Integer(ThumbnailHeight)),
            "-c:v", "png", "-f", "image2",
            outputPath,
        ], cancellationToken);
    }

    public Task<string> GetWaveformAsync(MediaAsset asset, CancellationToken cancellationToken) =>
        GetOrCreateAsync(asset, "wave", outputPath =>
        [
            "-v", "error", "-nostdin", "-y",
            "-i", FfmpegConventions.LocalFileProtocol + asset.Path,
            "-filter_complex", string.Concat("aformat=channel_layouts=mono,showwavespic=s=", WaveformSize, ":colors=white"),
            "-frames:v", "1",
            "-c:v", "png", "-f", "image2",
            outputPath,
        ], cancellationToken);

    private async Task<string> GetOrCreateAsync(
        MediaAsset asset,
        string kind,
        Func<string, string[]> buildArguments,
        CancellationToken cancellationToken)
    {
        var media = new FileInfo(asset.Path);
        if (!media.Exists)
        {
            throw new NotFoundException($"Falta el medio {asset.DisplayName}.");
        }

        Directory.CreateDirectory(config.CacheDirectory);
        var cachedPath = Path.Combine(config.CacheDirectory, string.Concat(CacheKey(media), "-", kind, ".png"));
        if (File.Exists(cachedPath))
        {
            return cachedPath;
        }

        var partialPath = string.Concat(cachedPath, ".", Guid.NewGuid().ToString("N"), ".part");
        try
        {
            var result = await processRunner.RunAsync(config.FfmpegPath, buildArguments(partialPath), config.MediaPreviewTimeout, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new ExternalServiceException($"ffmpeg no pudo generar la vista previa de {asset.DisplayName}.");
            }

            File.Move(partialPath, cachedPath, overwrite: true);
            return cachedPath;
        }
        finally
        {
            File.Delete(partialPath);
        }
    }

    private static string CacheKey(FileInfo media)
    {
        var identity = string.Concat(media.FullName, "|", Integer(media.Length), "|", Integer(media.LastWriteTimeUtc.Ticks));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..CacheKeyLength];
    }
}
