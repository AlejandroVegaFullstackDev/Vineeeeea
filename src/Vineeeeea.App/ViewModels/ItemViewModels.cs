using System;
using System.Collections.Generic;
using Avalonia.Media;
using Vineeeeea.App.Utils;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.App.ViewModels;

public sealed record AssetItemViewModel(Guid Id, string Name, string Details)
{
    public static AssetItemViewModel From(MediaAsset asset)
    {
        var duration = TimeFormatting.FormatMicroseconds(asset.DurationUs);
        var details = asset.Kind switch
        {
            MediaKind.Video => $"Video · {asset.Width}×{asset.Height} · {duration}{(asset.HasAudio ? " · con audio" : string.Empty)}",
            MediaKind.Image => $"Foto · {asset.Width}×{asset.Height}",
            _ => $"Audio · {duration}",
        };
        return new AssetItemViewModel(asset.Id, asset.DisplayName, details);
    }
}

public sealed record ClipItemViewModel(
    Guid Id,
    string Label,
    double Left,
    double Width,
    bool IsSelected,
    IImage? Thumbnail,
    IImage? Waveform);

public sealed record TrackItemViewModel(
    Guid Id,
    TrackKind Kind,
    string Name,
    string EasyName,
    string Status,
    bool IsVisual,
    bool IsMuted,
    bool IsHidden,
    bool IsLocked,
    IReadOnlyList<ClipItemViewModel> Clips,
    double ContentWidth,
    double AppendX)
{
    public bool IsVideoLane => Kind == TrackKind.Video;
    public bool IsTextLane => Kind == TrackKind.Text;
}

public sealed record EffectMenuEntry(string Type, string Label);

public sealed record RulerTickViewModel(double Left, string Label);
