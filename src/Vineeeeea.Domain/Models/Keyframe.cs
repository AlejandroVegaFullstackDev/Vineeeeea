using Vineeeeea.Domain.Errors;

namespace Vineeeeea.Domain.Models;

/// <summary>Anchored to the source media time, so trimming or splitting a clip never moves its animation.</summary>
public sealed record Keyframe
{
    public long SourceUs { get; }
    public double Value { get; }

    public Keyframe(long sourceUs, double value)
    {
        if (sourceUs < 0 || !double.IsFinite(value))
        {
            throw new ValidationException("Un keyframe necesita un tiempo no negativo y un valor finito.");
        }

        SourceUs = sourceUs;
        Value = value;
    }
}
