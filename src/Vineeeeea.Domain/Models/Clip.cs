using System.Collections.Immutable;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Domain.Models;

public sealed record Clip
{
    /// <summary>Speed conversions can land a microsecond past a frame boundary; that much never adds an extra frame.</summary>
    private const long FrameRoundingToleranceUs = 1;

    public Guid Id { get; }
    public Guid AssetId { get; }
    public long TimelineStartFrame { get; }
    public long SourceInUs { get; }
    public long SourceDurationUs { get; }
    public double Speed { get; }
    public ImmutableList<EffectInstance> Effects { get; }

    public Clip(
        Guid id,
        Guid assetId,
        long timelineStartFrame,
        long sourceInUs,
        long sourceDurationUs,
        double speed,
        IEnumerable<EffectInstance> effects)
    {
        if (timelineStartFrame < 0)
        {
            throw new ValidationException("Un clip no puede empezar antes del inicio de la línea de tiempo.");
        }

        if (sourceInUs < 0 || sourceDurationUs <= 0)
        {
            throw new ValidationException("El rango de origen del clip debe ser positivo.");
        }

        if (!double.IsFinite(speed) || speed < DomainLimits.MinSpeed || speed > DomainLimits.MaxSpeed)
        {
            throw new ValidationException($"La velocidad debe estar entre {DomainLimits.MinSpeed} y {DomainLimits.MaxSpeed}.");
        }

        var effectList = effects.ToImmutableList();
        if (effectList.Select(effect => effect.Id).Distinct().Count() != effectList.Count
            || effectList.Select(effect => effect.Type).Distinct().Count() != effectList.Count)
        {
            throw new ValidationException("Un clip no puede repetir un efecto ni su identificador.");
        }

        Id = NameRules.RequireId(id, "El clip");
        AssetId = NameRules.RequireId(assetId, "El medio del clip");
        TimelineStartFrame = timelineStartFrame;
        SourceInUs = sourceInUs;
        SourceDurationUs = sourceDurationUs;
        Speed = speed;
        Effects = effectList;
    }

    public long SourceEndUs => checked(SourceInUs + SourceDurationUs);

    public long TimelineDurationFrames(FrameRate frameRate)
    {
        var timelineUs = (long)Math.Round(SourceDurationUs / Speed);
        return Math.Max(1, frameRate.MicrosecondsToFramesCeiling(Math.Max(0, timelineUs - FrameRoundingToleranceUs)));
    }

    public long TimelineEndFrame(FrameRate frameRate) => checked(TimelineStartFrame + TimelineDurationFrames(frameRate));

    public long SourceUsAt(long timelineFrame, FrameRate frameRate) =>
        SourceInUs + (long)Math.Round(frameRate.FramesToMicroseconds(timelineFrame - TimelineStartFrame) * Speed);

    public EffectInstance? FindEffect(string type) => Effects.FirstOrDefault(effect => effect.Type == type);

    internal Clip WithTiming(long timelineStartFrame, long sourceInUs, long sourceDurationUs) =>
        new(Id, AssetId, timelineStartFrame, sourceInUs, sourceDurationUs, Speed, Effects);

    internal Clip WithAsset(Guid assetId) =>
        new(Id, assetId, TimelineStartFrame, SourceInUs, SourceDurationUs, Speed, Effects);

    internal Clip WithSpeed(double speed) =>
        new(Id, AssetId, TimelineStartFrame, SourceInUs, SourceDurationUs, speed, Effects);

    internal Clip WithEffects(IEnumerable<EffectInstance> effects) =>
        new(Id, AssetId, TimelineStartFrame, SourceInUs, SourceDurationUs, Speed, effects);

    internal Clip CopyAs(Guid id, long timelineStartFrame, long sourceInUs, long sourceDurationUs) =>
        new(id, AssetId, timelineStartFrame, sourceInUs, sourceDurationUs, Speed, Effects);
}
