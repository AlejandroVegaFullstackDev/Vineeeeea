using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Domain.Tests;

public class FrameRateTests
{
    private static readonly FrameRate Ntsc = new(30000, 1001);

    [Fact]
    public void Converts_ntsc_frames_without_drift()
    {
        Assert.Equal(1_001_000_000, Ntsc.FramesToMicroseconds(30000));
        Assert.Equal(30000, Ntsc.MicrosecondsToFramesFloor(1_001_000_000));
    }

    [Fact]
    public void Floor_and_ceiling_differ_for_partial_frames()
    {
        Assert.Equal(0, Ntsc.MicrosecondsToFramesFloor(1));
        Assert.Equal(1, Ntsc.MicrosecondsToFramesCeiling(1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(30, 0)]
    [InlineData(-30, 1)]
    public void Rejects_non_positive_fractions(int numerator, int denominator)
    {
        Assert.Throws<ValidationException>(() => new FrameRate(numerator, denominator));
    }
}
