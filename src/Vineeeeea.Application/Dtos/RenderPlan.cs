using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Dtos;

/// <summary>
/// A stretch of the timeline resolved into independent layers, ready for any render engine.
/// Video layers are listed bottom to top; frames and microseconds are relative to the start of the plan.
/// </summary>
public sealed record RenderPlan(
    FrameSize OutputSize,
    FrameRate FrameRate,
    int AudioSampleRate,
    long DurationFrames,
    IReadOnlyList<RenderLayer> Layers)
{
    public long DurationUs => FrameRate.FramesToMicroseconds(DurationFrames);

    public bool HasAudio => Layers.Any(layer => layer.ProducesAudio);
}

public sealed record RenderLayer(
    string MediaPath,
    long StartFrame,
    long DurationFrames,
    long SourceStartUs,
    long SourceDurationUs,
    double Speed,
    bool IsStillImage,
    bool ProducesVideo,
    bool ProducesAudio,
    IReadOnlyList<EffectInstance> Effects,
    FadeRamp? VideoRamp = null,
    FadeRamp? AudioRamp = null,
    TextStyle? Text = null);

/// <summary>
/// How strongly a layer shows over time: rising over <see cref="InUs"/> from the start of its clip and falling over
/// <see cref="OutUs"/> before its end. <see cref="OffsetUs"/> is how far into the clip the layer begins.
/// </summary>
public sealed record FadeRamp(long OffsetUs, long ClipDurationUs, long InUs, long OutUs);
