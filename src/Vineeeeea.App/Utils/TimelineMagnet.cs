using System;
using System.Collections.Generic;

namespace Vineeeeea.App.Utils;

/// <summary>
/// Snaps a dragged clip so its edges click onto nearby reference points (the start, the playhead, other clips'
/// borders) instead of leaving tiny gaps. Pure maths in frames so it can be reasoned about and tested on its own.
/// </summary>
public static class TimelineMagnet
{
    public static long SnapStart(long desiredStart, long durationFrames, IReadOnlyList<long> targets, long thresholdFrames)
    {
        var best = desiredStart;
        var bestDistance = thresholdFrames + 1;
        foreach (var target in targets)
        {
            Consider(target, Math.Abs(target - desiredStart));
            Consider(target - durationFrames, Math.Abs(target - (desiredStart + durationFrames)));
        }

        return Math.Max(0, best);

        void Consider(long candidateStart, long distance)
        {
            if (distance <= thresholdFrames && distance < bestDistance)
            {
                best = candidateStart;
                bestDistance = distance;
            }
        }
    }
}
