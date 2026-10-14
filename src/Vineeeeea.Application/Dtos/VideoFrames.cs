namespace Vineeeeea.Application.Dtos;

public sealed record FrameSize(int Width, int Height)
{
    public int FrameBytes => Width * Height * VideoFrame.BytesPerPixel;
}

/// <summary>Pixels are tightly packed BGRA rows; null pixels mean a black frame.</summary>
public sealed record VideoFrame(long TimelineFrame, FrameSize Size, byte[]? Bgra)
{
    public const int BytesPerPixel = 4;

    public static VideoFrame Black(long timelineFrame, FrameSize size) => new(timelineFrame, size, Bgra: null);
}
