using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Controllers;

public sealed class ClipPreviewController(IMediaPreviewCache previewCache)
{
    public Task<string?> GetThumbnailAsync(MediaAsset asset, CancellationToken cancellationToken) =>
        asset.Kind is MediaKind.Video or MediaKind.Image ? Nullable(previewCache.GetThumbnailAsync(asset, cancellationToken)) : Task.FromResult<string?>(null);

    public Task<string?> GetWaveformAsync(MediaAsset asset, CancellationToken cancellationToken) =>
        asset.HasAudio ? Nullable(previewCache.GetWaveformAsync(asset, cancellationToken)) : Task.FromResult<string?>(null);

    private static async Task<string?> Nullable(Task<string> pending) => await pending;
}
