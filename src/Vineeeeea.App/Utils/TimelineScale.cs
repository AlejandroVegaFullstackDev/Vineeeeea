using System;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.App.Utils;

public static class TimelineScale
{
    public const double PixelsPerSecond = 60;
    public const double TrailingSpacePixels = 400;

    public static double FramesToPixels(long frames, FrameRate frameRate) =>
        frames / frameRate.ToFramesPerSecond() * PixelsPerSecond;

    public static long PixelsToFrames(double pixels, FrameRate frameRate) =>
        (long)Math.Round(pixels / PixelsPerSecond * frameRate.ToFramesPerSecond());
}
