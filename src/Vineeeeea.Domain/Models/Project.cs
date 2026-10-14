using System.Collections.Immutable;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Domain.Models;

public sealed record Project
{
    public Guid Id { get; }
    public string Name { get; private init; }
    public ProjectSettings Settings { get; private init; }
    public ImmutableList<MediaAsset> Assets { get; private init; } = [];
    public ImmutableList<Track> Tracks { get; private init; } = [];

    public Project(Guid id, string name, ProjectSettings settings)
    {
        Id = NameRules.RequireId(id, "El proyecto");
        Name = NameRules.Normalize(name, "El nombre del proyecto");
        Settings = settings ?? throw new ValidationException("Faltan los ajustes del proyecto.");
    }

    public Project Rename(string name) => this with { Name = NameRules.Normalize(name, "El nombre del proyecto") };

    public Project AddAsset(MediaAsset asset)
    {
        if (Assets.Any(existing => existing.Id == asset.Id))
        {
            throw new ValidationException("Ya existe un medio con ese identificador.");
        }

        return this with { Assets = Assets.Add(asset) };
    }

    public Project AddTrack(Track track)
    {
        if (Tracks.Any(existing => existing.Id == track.Id))
        {
            throw new ValidationException("Ya existe una pista con ese identificador.");
        }

        if (!track.Clips.IsEmpty)
        {
            throw new BusinessRuleException("Una pista nueva debe añadirse vacía.");
        }

        return this with { Tracks = Tracks.Add(track) };
    }

    public Project InsertClip(Guid trackId, Clip clip)
    {
        EnsureClipIdIsFree(clip.Id);
        return PlaceClip(EnsureUnlocked(GetTrack(trackId)), clip);
    }

    public Project MoveClip(Guid clipId, Guid targetTrackId, long timelineStartFrame)
    {
        var (sourceTrack, clip) = RequireClip(clipId);
        EnsureUnlocked(sourceTrack);
        EnsureUnlocked(GetTrack(targetTrackId));
        var withoutClip = ReplaceTrack(sourceTrack.WithClips(sourceTrack.Clips.Remove(clip)));
        return withoutClip.PlaceClip(
            withoutClip.GetTrack(targetTrackId),
            clip.WithTiming(timelineStartFrame, clip.SourceInUs, clip.SourceDurationUs));
    }

    public Project ShiftClip(Guid clipId, long deltaFrames)
    {
        var (track, clip) = RequireClip(clipId);
        return MoveClip(clipId, track.Id, Math.Max(0, clip.TimelineStartFrame + deltaFrames));
    }

    public Project RemoveClip(Guid clipId)
    {
        var (track, clip) = RequireClip(clipId);
        EnsureUnlocked(track);
        return ReplaceTrack(track.WithClips(track.Clips.Remove(clip))).WithoutUnusedTexts();
    }

    public Project TrimClipStart(Guid clipId, long deltaFrames) => ChangeClip(clipId, clip =>
    {
        var deltaUs = SourceUsForFrames(deltaFrames, clip.Speed);
        return clip.WithTiming(clip.TimelineStartFrame + deltaFrames, clip.SourceInUs + deltaUs, clip.SourceDurationUs - deltaUs);
    });

    public Project TrimClipEnd(Guid clipId, long deltaFrames) => ChangeClip(clipId, clip =>
        clip.WithTiming(clip.TimelineStartFrame, clip.SourceInUs, clip.SourceDurationUs + SourceUsForFrames(deltaFrames, clip.Speed)));

    public Project SplitClip(Guid clipId, long atFrame, Guid newClipId)
    {
        EnsureClipIdIsFree(newClipId);
        var (track, clip) = RequireClip(clipId);
        EnsureUnlocked(track);
        if (atFrame <= clip.TimelineStartFrame || atFrame >= clip.TimelineEndFrame(Settings.FrameRate))
        {
            throw new BusinessRuleException("El cabezal debe estar dentro del clip para dividirlo.");
        }

        var firstDurationUs = SourceUsForFrames(atFrame - clip.TimelineStartFrame, clip.Speed);
        var first = clip.WithTiming(clip.TimelineStartFrame, clip.SourceInUs, firstDurationUs);
        var (withOwnText, secondAssetId) = WithOwnCopyOfText(clip.AssetId);
        var second = clip
            .CopyAs(newClipId, atFrame, clip.SourceInUs + firstDurationUs, clip.SourceDurationUs - firstDurationUs)
            .WithAsset(secondAssetId);

        var withoutClip = withOwnText.ReplaceTrack(track.WithClips(track.Clips.Remove(clip)));
        var withFirst = withoutClip.PlaceClip(withoutClip.GetTrack(track.Id), first);
        return withFirst.PlaceClip(withFirst.GetTrack(track.Id), second);
    }

    public Project DuplicateClip(Guid clipId, Guid newClipId)
    {
        EnsureClipIdIsFree(newClipId);
        var (track, clip) = RequireClip(clipId);
        EnsureUnlocked(track);
        var (withOwnText, copyAssetId) = WithOwnCopyOfText(clip.AssetId);
        var copy = clip
            .CopyAs(newClipId, clip.TimelineEndFrame(Settings.FrameRate), clip.SourceInUs, clip.SourceDurationUs)
            .WithAsset(copyAssetId);
        return withOwnText.PlaceClip(withOwnText.GetTrack(track.Id), copy);
    }

    public Project UpdateText(Guid assetId, TextStyle text)
    {
        var asset = GetAsset(assetId);
        return this with { Assets = Assets.Replace(asset, asset.WithText(text)) };
    }

    public Project RemoveAsset(Guid assetId)
    {
        var asset = GetAsset(assetId);
        return this with
        {
            Assets = Assets.Remove(asset),
            Tracks = Tracks.Select(track => track.WithClips(track.Clips.RemoveAll(clip => clip.AssetId == assetId))).ToImmutableList(),
        };
    }

    public Project RemoveTrack(Guid trackId) => (this with { Tracks = Tracks.Remove(GetTrack(trackId)) }).WithoutUnusedTexts();

    public Project SetTrackMuted(Guid trackId, bool isMuted) => ReplaceTrack(GetTrack(trackId).WithMuted(isMuted));

    public Project SetTrackHidden(Guid trackId, bool isHidden) => ReplaceTrack(GetTrack(trackId).WithHidden(isHidden));

    public Project SetTrackLocked(Guid trackId, bool isLocked) => ReplaceTrack(GetTrack(trackId).WithLocked(isLocked));

    public Project RemoveAllClipEffects(Guid clipId) => ChangeClip(clipId, clip => clip.WithEffects([]));

    /// <summary>
    /// Clips keep their place in time: positions are converted to the new frame rate, and a clip that would now
    /// touch the previous one by rounding moves forward by at most a frame.
    /// </summary>
    public Project ChangeSettings(ProjectSettings settings)
    {
        var previousRate = Settings.FrameRate;
        var nextRate = settings.FrameRate;
        var tracks = Tracks.Select(track =>
        {
            var previousEnd = 0L;
            var clips = track.Clips.Select(clip =>
            {
                var start = Math.Max(previousEnd, nextRate.MicrosecondsToFramesFloor(previousRate.FramesToMicroseconds(clip.TimelineStartFrame)));
                var moved = clip.WithTiming(start, clip.SourceInUs, clip.SourceDurationUs);
                previousEnd = moved.TimelineEndFrame(nextRate);
                return moved;
            });
            return track.WithClips(clips.ToImmutableList());
        });

        return this with { Settings = settings, Tracks = tracks.ToImmutableList() };
    }

    public Project SetClipSpeed(Guid clipId, double speed) => ChangeClip(clipId, clip => clip.WithSpeed(speed));

    /// <summary>Turns the clip another quarter turn clockwise, wrapping back to upright after a full turn.</summary>
    public Project RotateClipQuarterTurn(Guid clipId) => ChangeClip(clipId, clip =>
    {
        var current = clip.FindEffect(EffectCatalog.Rotate)?.Parameters[EffectCatalog.Degrees] ?? 0;
        var next = (current + EffectCatalog.QuarterTurnDegrees) % EffectCatalog.FullTurnDegrees;
        var effects = clip.Effects.RemoveAll(effect => effect.Type == EffectCatalog.Rotate);
        return clip.WithEffects(next == 0
            ? effects
            : effects.Add(EffectInstance.CreateDefault(EffectCatalog.Rotate).WithParameter(EffectCatalog.Degrees, next)));
    });

    public Project ApplyFilter(Guid clipId, FilterPreset preset) => ChangeClip(clipId, clip =>
    {
        var effects = WithoutColorEffects(clip.Effects);
        foreach (var group in preset.Settings.GroupBy(setting => setting.EffectType))
        {
            var effect = group.Aggregate(EffectInstance.CreateDefault(group.Key), (current, setting) => current.WithParameter(setting.Parameter, setting.Value));
            effects = effects.Add(effect);
        }

        return clip.WithEffects(effects);
    });

    public Project RemoveColorFilters(Guid clipId) => ChangeClip(clipId, clip => clip.WithEffects(WithoutColorEffects(clip.Effects)));

    public Project SetClipEffect(Guid clipId, EffectInstance effect) => ChangeClip(clipId, clip =>
        clip.WithEffects(clip.Effects.RemoveAll(existing => existing.Type == effect.Type).Add(effect)));

    public Project RemoveClipEffect(Guid clipId, string effectType) => ChangeClip(clipId, clip =>
        clip.WithEffects(clip.Effects.RemoveAll(existing => existing.Type == effectType)));

    public Track GetTrack(Guid trackId) =>
        Tracks.FirstOrDefault(track => track.Id == trackId) ?? throw new NotFoundException("La pista no existe.");

    public MediaAsset GetAsset(Guid assetId) =>
        Assets.FirstOrDefault(asset => asset.Id == assetId) ?? throw new NotFoundException("El medio no existe.");

    public Clip GetClip(Guid clipId) => RequireClip(clipId).Clip;

    public Clip? TryGetClip(Guid clipId) => FindClip(clipId)?.Clip;

    public Track GetTrackOfClip(Guid clipId) => RequireClip(clipId).Track;

    public long TrackEndFrame(Track track) =>
        track.Clips.Count == 0 ? 0 : track.Clips.Max(clip => clip.TimelineEndFrame(Settings.FrameRate));

    public long EndFrame() => Tracks.Count == 0 ? 0 : Tracks.Max(TrackEndFrame);

    private Project ChangeClip(Guid clipId, Func<Clip, Clip> change)
    {
        var (track, clip) = RequireClip(clipId);
        EnsureUnlocked(track);
        var withoutClip = ReplaceTrack(track.WithClips(track.Clips.Remove(clip)));
        return withoutClip.PlaceClip(withoutClip.GetTrack(track.Id), change(clip));
    }

    private static Track EnsureUnlocked(Track track) => track.IsLocked
        ? throw new BusinessRuleException($"La pista «{track.Name}» está bloqueada; desbloquéala para editarla.")
        : track;

    private Project PlaceClip(Track track, Clip clip)
    {
        var asset = GetAsset(clip.AssetId);
        if (!track.Accepts(asset.Kind))
        {
            throw new BusinessRuleException($"Una pista de {track.Kind} no admite medios de {asset.Kind}.");
        }

        if (clip.SourceEndUs > asset.DurationUs)
        {
            throw new BusinessRuleException("El rango de origen del clip supera la duración del medio.");
        }

        foreach (var effect in clip.Effects)
        {
            EnsureEffectFits(effect, asset);
        }

        var frameRate = Settings.FrameRate;
        var start = clip.TimelineStartFrame;
        var end = clip.TimelineEndFrame(frameRate);
        if (track.Clips.Any(other => start < other.TimelineEndFrame(frameRate) && other.TimelineStartFrame < end))
        {
            throw new BusinessRuleException("El clip se solapa con otro clip de la pista.");
        }

        return ReplaceTrack(track.WithClips(track.Clips.Add(clip)));
    }

    /// <summary>Each text clip owns its text, so copies made by splitting or duplicating can be edited on their own.</summary>
    private (Project Project, Guid AssetId) WithOwnCopyOfText(Guid assetId)
    {
        var asset = GetAsset(assetId);
        if (asset.Kind != MediaKind.Text)
        {
            return (this, assetId);
        }

        var copy = asset.CopyAs(Guid.NewGuid());
        return (this with { Assets = Assets.Add(copy) }, copy.Id);
    }

    private Project WithoutUnusedTexts()
    {
        var usedAssetIds = Tracks.SelectMany(track => track.Clips).Select(clip => clip.AssetId).ToHashSet();
        return this with { Assets = Assets.RemoveAll(asset => asset.Kind == MediaKind.Text && !usedAssetIds.Contains(asset.Id)) };
    }

    private static ImmutableList<EffectInstance> WithoutColorEffects(ImmutableList<EffectInstance> effects) =>
        effects.RemoveAll(effect => FilterPresets.ColorEffectTypes.Contains(effect.Type));

    private static void EnsureEffectFits(EffectInstance effect, MediaAsset asset)
    {
        if (!effect.Spec.AppliesTo(asset))
        {
            throw new BusinessRuleException($"El efecto {effect.Type} no se puede aplicar a {asset.DisplayName}.");
        }
    }

    private long SourceUsForFrames(long frames, double speed) =>
        (long)Math.Round(Settings.FrameRate.FramesToMicroseconds(frames) * speed);

    private Project ReplaceTrack(Track updated) =>
        this with { Tracks = Tracks.Replace(GetTrack(updated.Id), updated) };

    private void EnsureClipIdIsFree(Guid clipId)
    {
        if (FindClip(clipId) is not null)
        {
            throw new ValidationException("Ya existe un clip con ese identificador.");
        }
    }

    private (Track Track, Clip Clip) RequireClip(Guid clipId) =>
        FindClip(clipId) ?? throw new NotFoundException("El clip no existe.");

    private (Track Track, Clip Clip)? FindClip(Guid clipId)
    {
        foreach (var track in Tracks)
        {
            var clip = track.Clips.FirstOrDefault(candidate => candidate.Id == clipId);
            if (clip is not null)
            {
                return (track, clip);
            }
        }

        return null;
    }
}
