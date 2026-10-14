namespace Vineeeeea.Application.Services;

/// <summary>Saves a single frame of a media file (at a source timestamp) as an image, for the freeze-frame feature.</summary>
public interface IFrameExtractor
{
    Task<string> ExtractAsync(string mediaPath, long sourceUs, CancellationToken cancellationToken);
}
