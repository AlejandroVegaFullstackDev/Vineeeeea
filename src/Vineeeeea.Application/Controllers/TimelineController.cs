using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Application.Controllers;

public sealed class TimelineController(EditorSession session)
{
    public Track AddTrack(TrackKind kind)
    {
        var track = NewTrack(session.RequireProject(), kind);
        session.Apply(current => current.AddTrack(track));
        return track;
    }

    public Clip AppendClip(Guid trackId, Guid assetId)
    {
        var project = session.RequireProject();
        var clip = NewClip(project.GetAsset(assetId), project.TrackEndFrame(project.GetTrack(trackId)));
        session.Apply(current => current.InsertClip(trackId, clip));
        return clip;
    }

    /// <summary>
    /// Drops media at a frame on the given track; when there is no track or it holds another kind of media,
    /// a matching track is created in the same undo step.
    /// </summary>
    public Clip PlaceAsset(Guid assetId, Guid? trackId, long startFrame)
    {
        var project = session.RequireProject();
        var asset = project.GetAsset(assetId);
        var clip = NewClip(asset, Math.Max(0, startFrame));
        var target = trackId is { } id ? project.GetTrack(id) : null;
        if (target is not null && target.Accepts(asset.Kind))
        {
            session.Apply(current => current.InsertClip(target.Id, clip));
            return clip;
        }

        var track = NewTrack(project, TrackKindFor(asset.Kind));
        session.Apply(current => current.AddTrack(track).InsertClip(track.Id, clip));
        return clip;
    }

    public Clip AddText(long atFrame)
    {
        var text = MediaAsset.CreateText(Guid.NewGuid(), TextStyle.Default(DefaultTextContent));
        session.Apply(project => project.AddAsset(text));
        var project = session.RequireProject();
        var track = project.Tracks.LastOrDefault(candidate => candidate.Kind == TrackKind.Text);
        var clip = NewClip(text, Math.Max(0, atFrame));
        if (track is not null && !Overlaps(project, track, clip))
        {
            session.Apply(current => current.InsertClip(track.Id, clip), mergeKey: AddTextMergeKey(text.Id));
            return clip;
        }

        var newTrack = NewTrack(project, TrackKind.Text);
        session.Apply(current => current.AddTrack(newTrack).InsertClip(newTrack.Id, clip), mergeKey: AddTextMergeKey(text.Id));
        return clip;
    }

    public void UpdateText(Guid clipId, TextStyle text)
    {
        var assetId = session.RequireProject().GetClip(clipId).AssetId;
        session.Apply(project => project.UpdateText(assetId, text), mergeKey: $"{clipId}/text");
    }

    public Clip AppendAssetToTimeline(Guid assetId)
    {
        var project = session.RequireProject();
        var asset = project.GetAsset(assetId);
        var track = project.Tracks.LastOrDefault(candidate => candidate.Accepts(asset.Kind));
        return PlaceAsset(assetId, track?.Id, track is null ? 0 : project.TrackEndFrame(track));
    }

    public Guid DuplicateClip(Guid clipId)
    {
        var newClipId = Guid.NewGuid();
        session.Apply(project => project.DuplicateClip(clipId, newClipId));
        return newClipId;
    }

    public void SetTrackMuted(Guid trackId, bool isMuted) =>
        session.Apply(project => project.SetTrackMuted(trackId, isMuted));

    public void SetTrackHidden(Guid trackId, bool isHidden) =>
        session.Apply(project => project.SetTrackHidden(trackId, isHidden));

    public void SetTrackLocked(Guid trackId, bool isLocked) =>
        session.Apply(project => project.SetTrackLocked(trackId, isLocked));

    public void RemoveTrack(Guid trackId) => session.Apply(project => project.RemoveTrack(trackId));

    public void SetClipSpeed(Guid clipId, double speed) =>
        session.Apply(project => project.SetClipSpeed(clipId, speed), mergeKey: $"{clipId}/speed");

    public void RotateClipQuarterTurn(Guid clipId) =>
        session.Apply(project => project.RotateClipQuarterTurn(clipId));

    public void ApplyFilter(Guid clipId, string presetName) =>
        session.Apply(project => project.ApplyFilter(clipId, FilterPresets.Get(presetName)));

    public void RemoveColorFilters(Guid clipId) => session.Apply(project => project.RemoveColorFilters(clipId));

    public void RemoveAllEffects(Guid clipId) => session.Apply(project => project.RemoveAllClipEffects(clipId));

    public void MoveClip(Guid clipId, Guid targetTrackId, long timelineStartFrame) =>
        session.Apply(project => project.MoveClip(clipId, targetTrackId, timelineStartFrame));

    public void ShiftClip(Guid clipId, long deltaFrames) =>
        session.Apply(project => project.ShiftClip(clipId, deltaFrames));

    public void TrimClipStart(Guid clipId, long deltaFrames) =>
        session.Apply(project => project.TrimClipStart(clipId, deltaFrames));

    public void TrimClipEnd(Guid clipId, long deltaFrames) =>
        session.Apply(project => project.TrimClipEnd(clipId, deltaFrames));

    public Guid SplitClip(Guid clipId, long atFrame)
    {
        var newClipId = Guid.NewGuid();
        session.Apply(project => project.SplitClip(clipId, atFrame, newClipId));
        return newClipId;
    }

    public void RemoveClip(Guid clipId) => session.Apply(project => project.RemoveClip(clipId));

    /// <summary>
    /// Once a parameter is animated, editing it changes the keyframe under the playhead instead of the static value.
    /// </summary>
    public void SetEffectParameter(Guid clipId, string effectType, string parameter, double value, long playheadFrame)
    {
        var project = session.RequireProject();
        var clip = project.GetClip(clipId);
        var effect = clip.FindEffect(effectType) ?? EffectInstance.CreateDefault(effectType);
        var updated = effect.Keyframes.ContainsKey(parameter)
            ? effect.WithKeyframe(parameter, SourceUsUnderPlayhead(project, clip, playheadFrame), value)
            : effect.WithParameter(parameter, value);
        session.Apply(current => current.SetClipEffect(clipId, updated), mergeKey: $"{clipId}/{effectType}/{parameter}");
    }

    public void AddKeyframe(Guid clipId, string effectType, string parameter, long playheadFrame)
    {
        var project = session.RequireProject();
        var clip = project.GetClip(clipId);
        var effect = clip.FindEffect(effectType) ?? EffectInstance.CreateDefault(effectType);
        var sourceUs = SourceUsUnderPlayhead(project, clip, playheadFrame);
        var updated = effect.WithKeyframe(parameter, sourceUs, effect.ValueAt(parameter, sourceUs));
        session.Apply(current => current.SetClipEffect(clipId, updated));
    }

    public void ClearKeyframes(Guid clipId, string effectType, string parameter)
    {
        var effect = session.RequireProject().GetClip(clipId).FindEffect(effectType);
        if (effect is not null)
        {
            session.Apply(project => project.SetClipEffect(clipId, effect.WithoutKeyframes(parameter)));
        }
    }

    public void ResetEffect(Guid clipId, string effectType) =>
        session.Apply(project => project.RemoveClipEffect(clipId, effectType));

    /// <summary>
    /// Writes a whole camera move (keyframes on scale and position) in one undo step, spread across the clip's own
    /// source range so trimming or moving it never changes the motion.
    /// </summary>
    public void ApplyCameraMove(Guid clipId, CameraMove move)
    {
        var project = session.RequireProject();
        var clip = project.GetClip(clipId);
        var effect = clip.FindEffect(EffectCatalog.Transform) ?? EffectInstance.CreateDefault(EffectCatalog.Transform);
        effect = move switch
        {
            CameraMove.ZoomIn => Animate(effect, clip, (EffectCatalog.Scale, 1, CameraZoom)),
            CameraMove.ZoomOut => Animate(effect, clip, (EffectCatalog.Scale, CameraZoom, 1)),
            CameraMove.PanAcross => Animate(effect, clip, (EffectCatalog.Scale, CameraPanZoom, CameraPanZoom), (EffectCatalog.PositionX, -1, 1)),
            _ => effect
                .WithoutKeyframes(EffectCatalog.Scale)
                .WithoutKeyframes(EffectCatalog.PositionX)
                .WithoutKeyframes(EffectCatalog.PositionY),
        };

        session.Apply(current => current.SetClipEffect(clipId, effect));
    }

    private const string DefaultTextContent = "Escribe tu texto";

    private const double CameraZoom = 1.35;
    private const double CameraPanZoom = 1.3;

    private static EffectInstance Animate(EffectInstance effect, Clip clip, params (string Parameter, double From, double To)[] moves) =>
        moves.Aggregate(effect, (current, move) => current
            .WithKeyframe(move.Parameter, clip.SourceInUs, move.From)
            .WithKeyframe(move.Parameter, clip.SourceEndUs, move.To));

    private static string AddTextMergeKey(Guid textId) => $"{textId}/add";

    private static TrackKind TrackKindFor(MediaKind mediaKind) => mediaKind switch
    {
        MediaKind.Audio => TrackKind.Audio,
        MediaKind.Text => TrackKind.Text,
        _ => TrackKind.Video,
    };

    private static bool Overlaps(Project project, Track track, Clip clip)
    {
        var frameRate = project.Settings.FrameRate;
        return track.Clips.Any(other =>
            clip.TimelineStartFrame < other.TimelineEndFrame(frameRate) && other.TimelineStartFrame < clip.TimelineEndFrame(frameRate));
    }

    private static Track NewTrack(Project project, TrackKind kind)
    {
        var sameKindCount = project.Tracks.Count(track => track.Kind == kind);
        var label = kind switch
        {
            TrackKind.Audio => "Audio",
            TrackKind.Text => "Texto",
            _ => "Video",
        };
        return new Track(Guid.NewGuid(), kind, $"{label} {sameKindCount + 1}", isMuted: false, isHidden: false);
    }

    private static Clip NewClip(MediaAsset asset, long startFrame) =>
        new(Guid.NewGuid(), asset.Id, startFrame, sourceInUs: 0, asset.DefaultClipDurationUs, DomainLimits.NormalSpeed, effects: []);

    private static long SourceUsUnderPlayhead(Project project, Clip clip, long playheadFrame)
    {
        if (playheadFrame < clip.TimelineStartFrame || playheadFrame >= clip.TimelineEndFrame(project.Settings.FrameRate))
        {
            throw new BusinessRuleException("Coloca el cabezal sobre el clip para animarlo.");
        }

        return clip.SourceUsAt(playheadFrame, project.Settings.FrameRate);
    }
}
