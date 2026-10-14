using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Domain.Utils;

public static class EffectCatalog
{
    public const string Transform = "transform";
    public const string Crop = "crop";
    public const string Opacity = "opacity";
    public const string Brightness = "brightness";
    public const string Contrast = "contrast";
    public const string Saturation = "saturation";
    public const string Temperature = "temperature";
    public const string Grayscale = "grayscale";
    public const string Blur = "blur";
    public const string Sharpen = "sharpen";
    public const string Vignette = "vignette";
    public const string Fade = "fade";
    public const string Dissolve = "dissolve";
    public const string Rotate = "rotate";
    public const string Flip = "flip";
    public const string Reverse = "reverse";
    public const string Fill = "fill";
    public const string ChromaKey = "chromakey";
    public const string Volume = "volume";
    public const string AudioFade = "audiofade";
    public const string Denoise = "denoise";
    public const string Normalize = "normalize";

    public const string Amount = "amount";
    public const string Degrees = "degrees";
    public const string Horizontal = "horizontal";
    public const string Vertical = "vertical";
    public const string Similarity = "similarity";
    public const string Blend = "blend";
    public const string Target = "target";
    public const string Gain = "gain";
    public const string PositionX = "x";
    public const string PositionY = "y";
    public const string Scale = "scale";
    public const string Left = "left";
    public const string Top = "top";
    public const string Right = "right";
    public const string Bottom = "bottom";
    public const string Kelvin = "kelvin";
    public const string InSeconds = "in";
    public const string OutSeconds = "out";
    public const string Seconds = "seconds";

    public const double NeutralKelvin = 6500;
    public const double QuarterTurnDegrees = 90;
    public const double FullTurnDegrees = 360;
    public const double DefaultLoudnessLufs = -14;

    private const double MaxCropFraction = 0.45;
    private const double MaxFadeSeconds = 5;
    private const double MaxDissolveSeconds = 2;
    private const double DefaultFillBlur = 0.6;
    private const double DefaultChromaSimilarity = 0.30;
    private const double DefaultChromaBlend = 0.10;
    private const double MinLoudnessLufs = -30;
    private const double MaxLoudnessLufs = -9;

    public static readonly IReadOnlyList<EffectSpec> All =
    [
        new(Transform, MediaKind.Video,
        [
            new(PositionX, -1, 1, 0, Keyframable: true),
            new(PositionY, -1, 1, 0, Keyframable: true),
            new(Scale, 0.1, 5, 1, Keyframable: true),
        ]),
        new(Crop, MediaKind.Video,
        [
            new(Left, 0, MaxCropFraction, 0, Keyframable: false),
            new(Top, 0, MaxCropFraction, 0, Keyframable: false),
            new(Right, 0, MaxCropFraction, 0, Keyframable: false),
            new(Bottom, 0, MaxCropFraction, 0, Keyframable: false),
        ]),
        new(Opacity, MediaKind.Video, [new(Amount, 0, 1, 1, Keyframable: false)]),
        new(Brightness, MediaKind.Video, [new(Amount, -1, 1, 0, Keyframable: true)]),
        new(Contrast, MediaKind.Video, [new(Amount, 0, 3, 1, Keyframable: true)]),
        new(Saturation, MediaKind.Video, [new(Amount, 0, 3, 1, Keyframable: true)]),
        new(Temperature, MediaKind.Video, [new(Kelvin, 2000, 12000, NeutralKelvin, Keyframable: false)]),
        new(Grayscale, MediaKind.Video, [new(Amount, 0, 1, 0, Keyframable: false)]),
        new(Blur, MediaKind.Video, [new(Amount, 0, 20, 0, Keyframable: false)]),
        new(Sharpen, MediaKind.Video, [new(Amount, 0, 2, 0, Keyframable: false)]),
        new(Vignette, MediaKind.Video, [new(Amount, 0, 1, 0, Keyframable: false)]),
        new(Rotate, MediaKind.Video, [new(Degrees, 0, 270, 0, Keyframable: false)]),
        new(Flip, MediaKind.Video,
        [
            new(Horizontal, 0, 1, 0, Keyframable: false),
            new(Vertical, 0, 1, 0, Keyframable: false),
        ]),
        new(Reverse, MediaKind.Video, [new(Amount, 0, 1, 0, Keyframable: false)]),
        new(Fill, MediaKind.Video, [new(Amount, 0, 1, DefaultFillBlur, Keyframable: false)]),
        new(ChromaKey, MediaKind.Video,
        [
            new(Similarity, 0.01, 1, DefaultChromaSimilarity, Keyframable: false),
            new(Blend, 0, 1, DefaultChromaBlend, Keyframable: false),
        ]),
        new(Fade, MediaKind.Video,
        [
            new(InSeconds, 0, MaxFadeSeconds, 0, Keyframable: false),
            new(OutSeconds, 0, MaxFadeSeconds, 0, Keyframable: false),
        ]),
        new(Dissolve, MediaKind.Video, [new(Seconds, 0, MaxDissolveSeconds, 0, Keyframable: false)]),
        new(Volume, MediaKind.Audio, [new(Gain, 0, 4, 1, Keyframable: true)]),
        new(AudioFade, MediaKind.Audio,
        [
            new(InSeconds, 0, MaxFadeSeconds, 0, Keyframable: false),
            new(OutSeconds, 0, MaxFadeSeconds, 0, Keyframable: false),
        ]),
        new(Denoise, MediaKind.Audio, [new(Amount, 0, 1, 0, Keyframable: false)]),
        new(Normalize, MediaKind.Audio, [new(Target, MinLoudnessLufs, MaxLoudnessLufs, DefaultLoudnessLufs, Keyframable: false)]),
    ];

    public static EffectSpec Get(string type) =>
        All.FirstOrDefault(spec => spec.Type == type) ?? throw new ValidationException($"El efecto {type} no existe.");
}
