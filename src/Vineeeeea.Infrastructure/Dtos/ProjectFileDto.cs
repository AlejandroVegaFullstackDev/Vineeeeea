using Vineeeeea.Domain.Models;

namespace Vineeeeea.Infrastructure.Dtos;

internal sealed record ProjectFileDto(
    string Format,
    int SchemaVersion,
    Guid Id,
    string Name,
    ProjectSettingsDto Settings,
    IReadOnlyList<MediaAssetDto> Assets,
    IReadOnlyList<TrackDto> Tracks);

internal sealed record ProjectSettingsDto(
    int Width,
    int Height,
    int FrameRateNumerator,
    int FrameRateDenominator,
    int AudioSampleRate);

internal sealed record MediaAssetDto(
    Guid Id,
    MediaKind Kind,
    string Path,
    long DurationUs,
    int? Width,
    int? Height,
    bool HasAudio,
    TextStyleDto? Text = null);

internal sealed record TextStyleDto(string Content, double Size, string Color, bool HasBackground, bool IsBold);

internal sealed record TrackDto(
    Guid Id,
    TrackKind Kind,
    string Name,
    bool IsMuted,
    bool IsHidden,
    IReadOnlyList<ClipDto> Clips,
    bool IsLocked = false);

internal sealed record ClipDto(
    Guid Id,
    Guid AssetId,
    long TimelineStartFrame,
    long SourceInUs,
    long SourceDurationUs,
    double Speed,
    IReadOnlyList<EffectDto> Effects);

internal sealed record EffectDto(
    Guid Id,
    string Type,
    bool Enabled,
    IReadOnlyDictionary<string, double> Parameters,
    IReadOnlyDictionary<string, IReadOnlyList<KeyframeDto>>? Keyframes = null);

internal sealed record KeyframeDto(long SourceUs, double Value);
