using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Controllers;

public sealed class MediaImportController(EditorSession session, IMediaProbe probe)
{
    private const string ImageSequenceFormat = "image2";
    private const string SingleImageFormatSuffix = "_pipe";

    public async Task<MediaAsset> ImportAsync(string filePath, CancellationToken cancellationToken)
    {
        session.RequireProject();
        var mediaPath = FileConventions.RequireAbsolutePath(filePath);
        var probed = await probe.ProbeAsync(mediaPath, cancellationToken);
        var asset = ToAsset(mediaPath, probed);
        session.Apply(project => project.AddAsset(asset));
        return asset;
    }

    private static MediaAsset ToAsset(string mediaPath, ProbedMedia probed)
    {
        var video = probed.Streams.FirstOrDefault(stream => stream.Type == ProbedStreamType.Video && !stream.IsAttachedPicture);
        var hasAudio = probed.Streams.Any(stream => stream.Type == ProbedStreamType.Audio);

        if (video is not null && IsStillImage(probed))
        {
            return MediaAsset.StillImage(
                Guid.NewGuid(),
                mediaPath,
                video.Width ?? throw new ValidationException("La imagen no indica su tamaño."),
                video.Height ?? throw new ValidationException("La imagen no indica su tamaño."));
        }

        if (probed.DurationUs is not > 0)
        {
            throw new ValidationException("El archivo no tiene una duración válida.");
        }

        if (video is not null)
        {
            return new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaPath, probed.DurationUs.Value, video.Width, video.Height, hasAudio);
        }

        if (hasAudio)
        {
            return new MediaAsset(Guid.NewGuid(), MediaKind.Audio, mediaPath, probed.DurationUs.Value, width: null, height: null, hasAudio: true);
        }

        throw new ValidationException("El archivo no contiene video, audio ni imagen.");
    }

    private static bool IsStillImage(ProbedMedia probed) =>
        probed.FormatName is { } format
        && (format == ImageSequenceFormat || format.EndsWith(SingleImageFormatSuffix, StringComparison.Ordinal));
}
