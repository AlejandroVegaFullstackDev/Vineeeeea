using Vineeeeea.App.Utils;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.App.Tests;

public class TimeFormattingTests
{
    private static readonly FrameRate ThirtyFps = new(30, 1);

    [Theory]
    [InlineData(0, "00:00:00:00")]
    [InlineData(5, "00:00:00:05")]
    [InlineData(90, "00:00:03:00")]
    [InlineData(95, "00:00:03:05")]
    [InlineData(1830, "00:01:01:00")]
    public void Timecode_reads_as_hours_minutes_seconds_and_frames(long frame, string expected)
    {
        Assert.Equal(expected, TimeFormatting.FormatTimecode(frame, ThirtyFps));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(2_000_000, "0:02")]
    [InlineData(62_000_000, "1:02")]
    public void Easy_time_reads_as_minutes_and_seconds(long microseconds, string expected)
    {
        Assert.Equal(expected, TimeFormatting.FormatSeconds(microseconds));
    }
}
