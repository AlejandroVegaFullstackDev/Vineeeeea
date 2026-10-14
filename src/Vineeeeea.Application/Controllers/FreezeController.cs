using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Controllers;

public sealed class FreezeController(EditorSession session, IFrameExtractor extractor)
{
    private const double DefaultHoldSeconds = 2;

    /// <summary>Freezes the frame under the playhead for a couple of seconds; one undoable step.</summary>
    public async Task FreezeAtPlayheadAsync(Guid clipId, long playheadFrame, CancellationToken cancellationToken)
    {
        var project = session.RequireProject();
        var clip = project.GetClip(clipId);
        var asset = project.GetAsset(clip.AssetId);
        if (asset.Kind != MediaKind.Video)
        {
            throw new BusinessRuleException("Solo se puede congelar un video.");
        }

        var frameRate = project.Settings.FrameRate;
        if (playheadFrame <= clip.TimelineStartFrame || playheadFrame >= clip.TimelineEndFrame(frameRate))
        {
            throw new BusinessRuleException("Coloca el cabezal dentro del clip para congelarlo.");
        }

        var sourceUs = clip.SourceUsAt(playheadFrame, frameRate);
        var imagePath = await extractor.ExtractAsync(asset.Path, sourceUs, cancellationToken);
        var image = MediaAsset.StillImage(Guid.NewGuid(), imagePath, asset.Width ?? 1, asset.Height ?? 1);
        var holdFrames = Math.Max(1, (long)Math.Round(DefaultHoldSeconds * frameRate.ToFramesPerSecond()));

        session.Apply(current => current.FreezeFrame(clipId, playheadFrame, image, Guid.NewGuid(), Guid.NewGuid(), holdFrames));
    }
}
