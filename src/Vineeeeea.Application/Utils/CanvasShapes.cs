namespace Vineeeeea.Application.Utils;

public enum CanvasShape
{
    Horizontal,
    Vertical,
    Square,
}

public static class CanvasShapes
{
    private const int LongSide = 1920;
    private const int ShortSide = 1080;

    public static (int Width, int Height) SizeOf(CanvasShape shape) => shape switch
    {
        CanvasShape.Vertical => (ShortSide, LongSide),
        CanvasShape.Square => (ShortSide, ShortSide),
        _ => (LongSide, ShortSide),
    };

    public static CanvasShape ShapeOf(int width, int height) =>
        width > height ? CanvasShape.Horizontal
        : height > width ? CanvasShape.Vertical
        : CanvasShape.Square;
}
