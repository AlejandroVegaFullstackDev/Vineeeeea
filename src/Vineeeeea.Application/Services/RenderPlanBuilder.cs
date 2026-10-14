using Vineeeeea.Application.Dtos;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Application.Services;

public static class RenderPlanBuilder
{
    public static RenderPlan Build(Project project, long fromFrame, long toFrame, FrameSize outputSize)
    {
        if (fromFrame < 0 || toFrame <= fromFrame)
        {
            throw new BusinessRuleException("El rango a renderizar está vacío.");
        }

        var frameRate = project.Settings.FrameRate;
        var layers = new List<RenderLayer>();
        foreach (var track in project.Tracks)
        {
            var trackShowsVideo = track.IsVisual && !track.IsHidden;
            Clip? previous = null;
            foreach (var clip in track.Clips)
            {
                var asset = project.GetAsset(clip.AssetId);
                var producesAudio = asset.HasAudio && !track.IsMuted;
                var leadFrames = trackShowsVideo ? DissolveLeadFrames(clip, previous, frameRate) : 0;
                var range = new LayerRange(fromFrame, toFrame, frameRate);

                if (leadFrames > 0)
                {
                    AddLayer(layers, range, clip, asset, clip.TimelineStartFrame - leadFrames, producesVideo: true, producesAudio: false);
                    AddLayer(layers, range, clip, asset, clip.TimelineStartFrame, producesVideo: false, producesAudio);
                }
                else
                {
                    AddLayer(layers, range, clip, asset, clip.TimelineStartFrame, trackShowsVideo, producesAudio);
                }

                previous = clip;
            }
        }

        return new RenderPlan(outputSize, frameRate, project.Settings.AudioSampleRate, toFrame - fromFrame, layers);
    }

    private sealed record LayerRange(long FromFrame, long ToFrame, FrameRate FrameRate);

    private static void AddLayer(
        List<RenderLayer> layers,
        LayerRange range,
        Clip clip,
        MediaAsset asset,
        long visibleStartFrame,
        bool producesVideo,
        bool producesAudio)
    {
        var frameRate = range.FrameRate;
        var clipEnd = clip.TimelineEndFrame(frameRate);
        var start = Math.Max(visibleStartFrame, range.FromFrame);
        var end = Math.Min(clipEnd, range.ToFrame);
        if ((!producesVideo && !producesAudio) || start >= end)
        {
            return;
        }

        var sourceStartUs = clip.SourceUsAt(start, frameRate);
        var sourceDurationUs = Math.Min(
            clip.SourceEndUs - sourceStartUs,
            (long)Math.Round(frameRate.FramesToMicroseconds(end - start) * clip.Speed));
        var offsetUs = frameRate.FramesToMicroseconds(start - visibleStartFrame);
        var visibleDurationUs = frameRate.FramesToMicroseconds(clipEnd - visibleStartFrame);
        var dissolveUs = frameRate.FramesToMicroseconds(clip.TimelineStartFrame - visibleStartFrame);

        layers.Add(new RenderLayer(
            asset.Path,
            start - range.FromFrame,
            end - start,
            sourceStartUs,
            sourceDurationUs,
            clip.Speed,
            asset.Kind == MediaKind.Image,
            producesVideo,
            producesAudio,
            clip.Effects.Where(effect => effect.Enabled).ToList(),
            producesVideo ? VideoRamp(clip, offsetUs, visibleDurationUs, dissolveUs) : null,
            producesAudio ? AudioRamp(clip, frameRate.FramesToMicroseconds(start - clip.TimelineStartFrame), frameRate.FramesToMicroseconds(clipEnd - clip.TimelineStartFrame)) : null,
            asset.Text));
    }

    /// <summary>
    /// A dissolve draws the incoming clip over the end of the clip right before it; without such a neighbour,
    /// or without earlier source media to show, it falls back to a plain fade-in.
    /// </summary>
    private static long DissolveLeadFrames(Clip clip, Clip? previous, FrameRate frameRate)
    {
        var seconds = clip.FindEffect(EffectCatalog.Dissolve) is { Enabled: true } dissolve ? dissolve.Parameters[EffectCatalog.Seconds] : 0;
        if (seconds <= 0 || previous is null || previous.TimelineEndFrame(frameRate) != clip.TimelineStartFrame)
        {
            return 0;
        }

        var wantedFrames = frameRate.MicrosecondsToFramesFloor(SecondsToUs(seconds));
        var availableFrames = frameRate.MicrosecondsToFramesFloor((long)(clip.SourceInUs / clip.Speed));
        return Math.Min(Math.Min(wantedFrames, availableFrames), clip.TimelineStartFrame - previous.TimelineStartFrame);
    }

    private static FadeRamp? VideoRamp(Clip clip, long offsetUs, long visibleDurationUs, long dissolveUs)
    {
        var fade = clip.FindEffect(EffectCatalog.Fade) is { Enabled: true } effect ? effect : null;
        var dissolve = clip.FindEffect(EffectCatalog.Dissolve) is { Enabled: true } transition ? transition : null;
        var fadeInUs = SecondsToUs(fade?.Parameters[EffectCatalog.InSeconds] ?? 0);
        var dissolveAsFadeInUs = dissolveUs > 0 ? 0 : SecondsToUs(dissolve?.Parameters[EffectCatalog.Seconds] ?? 0);
        var outUs = SecondsToUs(fade?.Parameters[EffectCatalog.OutSeconds] ?? 0);
        return Ramp(offsetUs, visibleDurationUs, Math.Max(dissolveUs, Math.Max(fadeInUs, dissolveAsFadeInUs)), outUs);
    }

    private static FadeRamp? AudioRamp(Clip clip, long offsetUs, long clipDurationUs)
    {
        var fade = clip.FindEffect(EffectCatalog.AudioFade) is { Enabled: true } effect ? effect : null;
        return Ramp(
            offsetUs,
            clipDurationUs,
            SecondsToUs(fade?.Parameters[EffectCatalog.InSeconds] ?? 0),
            SecondsToUs(fade?.Parameters[EffectCatalog.OutSeconds] ?? 0));
    }

    private static FadeRamp? Ramp(long offsetUs, long clipDurationUs, long inUs, long outUs) =>
        inUs > 0 || outUs > 0 ? new FadeRamp(offsetUs, clipDurationUs, inUs, outUs) : null;

    private static long SecondsToUs(double seconds) => (long)Math.Round(seconds * TimeUnits.MicrosecondsPerSecond);
}
