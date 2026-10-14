using System;
using System.Globalization;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.App.Utils;

public static class TimeFormatting
{
    public static string FormatMicroseconds(long microseconds) =>
        TimeSpan.FromTicks(microseconds * TimeSpan.TicksPerSecond / TimeUnits.MicrosecondsPerSecond)
            .ToString(@"hh\:mm\:ss\.f", CultureInfo.InvariantCulture);

    /// <summary>Human time for the easy mode: minutes and seconds, no frames.</summary>
    public static string FormatSeconds(long microseconds)
    {
        var totalSeconds = microseconds / TimeUnits.MicrosecondsPerSecond;
        return string.Create(CultureInfo.InvariantCulture, $"{totalSeconds / 60}:{totalSeconds % 60:00}");
    }

    /// <summary>Editing timecode for the advanced mode: HH:MM:SS:FF with the frame count within the second.</summary>
    public static string FormatTimecode(long frame, FrameRate frameRate)
    {
        var fps = Math.Max(1, (int)Math.Round(frameRate.ToFramesPerSecond()));
        var totalSeconds = frame / fps;
        var frames = frame % fps;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalSeconds / 3600:00}:{totalSeconds / 60 % 60:00}:{totalSeconds % 60:00}:{frames:00}");
    }
}
