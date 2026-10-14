using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Domain.Models;

public sealed record FrameRate
{
    public int Numerator { get; }
    public int Denominator { get; }

    public FrameRate(int numerator, int denominator)
    {
        if (numerator <= 0 || denominator <= 0)
        {
            throw new ValidationException("La frecuencia de fotogramas debe ser una fracción positiva.");
        }

        Numerator = numerator;
        Denominator = denominator;
    }

    public long FramesToMicroseconds(long frames) =>
        checked(frames * TimeUnits.MicrosecondsPerSecond * Denominator / Numerator);

    /// <summary>Positions round down so a frame never starts after its timestamp.</summary>
    public long MicrosecondsToFramesFloor(long microseconds) =>
        checked(microseconds * Numerator / (TimeUnits.MicrosecondsPerSecond * Denominator));

    /// <summary>Durations round up so the last partial frame of a source is still shown.</summary>
    public long MicrosecondsToFramesCeiling(long microseconds)
    {
        var divisor = checked(TimeUnits.MicrosecondsPerSecond * Denominator);
        return checked((microseconds * Numerator + divisor - 1) / divisor);
    }

    public double ToFramesPerSecond() => (double)Numerator / Denominator;
}
