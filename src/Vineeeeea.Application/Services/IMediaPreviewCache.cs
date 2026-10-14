using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Services;

public interface IMediaPreviewCache
{
    Task<string> GetThumbnailAsync(MediaAsset asset, CancellationToken cancellationToken);

    Task<string> GetWaveformAsync(MediaAsset asset, CancellationToken cancellationToken);
}
