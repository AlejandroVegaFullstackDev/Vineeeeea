using Vineeeeea.Application.Config;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Infrastructure.Services;
using Vineeeeea.Infrastructure.Utils;
using static Vineeeeea.Infrastructure.Utils.FfmpegExpressions;

namespace Vineeeeea.Infrastructure.Implements;

public sealed class FfmpegFrameExtractor(IToolRunner processRunner, AppConfig config) : IFrameExtractor
{
    public async Task<string> ExtractAsync(string mediaPath, long sourceUs, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(config.CacheDirectory);
        var outputPath = Path.Combine(config.CacheDirectory, $"congelado-{Guid.NewGuid():N}.png");
        List<string> arguments =
        [
            "-v", "error", "-y",
            "-ss", Seconds(sourceUs),
            "-i", FfmpegConventions.LocalFileProtocol + mediaPath,
            "-frames:v", "1",
            outputPath,
        ];

        var result = await processRunner.RunAsync(config.FfmpegPath, arguments, config.MediaPreviewTimeout, cancellationToken);
        if (result.ExitCode != 0 || !File.Exists(outputPath))
        {
            throw new ExternalServiceException("No se pudo congelar el fotograma.");
        }

        return outputPath;
    }
}
