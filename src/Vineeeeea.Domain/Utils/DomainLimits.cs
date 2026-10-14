namespace Vineeeeea.Domain.Utils;

public static class DomainLimits
{
    public const int MaxNameLength = 200;
    public const int MaxCanvasSide = 8192;
    public const int MaxAudioSampleRate = 384_000;
    public const double NormalSpeed = 1.0;
    public const double MinSpeed = 0.1;
    public const double MaxSpeed = 100;
    public const long DefaultImageClipUs = 5_000_000;
    public const long DefaultTextClipUs = 3_000_000;
    public const int MaxTextLength = 500;
    public const double MinTextSize = 0.02;
    public const double MaxTextSize = 0.3;
    public const double DefaultTextSize = 0.08;
    public const string DefaultTextColor = "#FFFFFF";

    /// <summary>A still image has no length of its own; this is how far a clip of it may be stretched.</summary>
    public const long StillImageSourceUs = 3_600_000_000;
}
