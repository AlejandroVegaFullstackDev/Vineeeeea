using System.Collections.Generic;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.App.Utils;

public sealed record CanvasPreset(string Name, int Width, int Height)
{
    public override string ToString() => Name;
}

public sealed record FrameRatePreset(string Name, FrameRate FrameRate)
{
    public override string ToString() => Name;
}

public static class FormatPresets
{
    public static readonly IReadOnlyList<CanvasPreset> Canvases =
    [
        new("16:9 · Horizontal (YouTube)", 1920, 1080),
        new("9:16 · Vertical (TikTok, Reels, Shorts)", 1080, 1920),
        new("1:1 · Cuadrado", 1080, 1080),
        new("4:5 · Retrato (Instagram)", 1080, 1350),
        new("4:3 · Clásico", 1440, 1080),
        new("21:9 · Cine", 2560, 1080),
    ];

    public static readonly IReadOnlyList<FrameRatePreset> FrameRates =
    [
        new("24 fps · Cine", new FrameRate(24, 1)),
        new("25 fps", new FrameRate(25, 1)),
        new("29.97 fps", new FrameRate(30000, 1001)),
        new("30 fps", new FrameRate(30, 1)),
        new("50 fps", new FrameRate(50, 1)),
        new("60 fps · Fluido", new FrameRate(60, 1)),
    ];
}
