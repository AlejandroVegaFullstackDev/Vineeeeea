using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Vineeeeea.Application.Dtos;

namespace Vineeeeea.App.Services;

/// <summary>
/// Alternates between two bitmaps: the one on screen is never written to, and handing the Image a different
/// instance each frame is what makes it redraw without allocating a new bitmap per frame.
/// </summary>
public sealed class PreviewBitmapWriter
{
    private const double ScreenDpi = 96;

    private readonly WriteableBitmap?[] buffers = new WriteableBitmap?[2];
    private byte[] blackRow = [];
    private int nextBuffer;

    public Bitmap Write(VideoFrame frame)
    {
        var pixelSize = new PixelSize(frame.Size.Width, frame.Size.Height);
        var bitmap = buffers[nextBuffer];
        if (bitmap is null || bitmap.PixelSize != pixelSize)
        {
            bitmap?.Dispose();
            bitmap = new WriteableBitmap(pixelSize, new Vector(ScreenDpi, ScreenDpi), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            buffers[nextBuffer] = bitmap;
        }

        var sourceRowBytes = frame.Size.Width * VideoFrame.BytesPerPixel;
        if (blackRow.Length != sourceRowBytes)
        {
            blackRow = new byte[sourceRowBytes];
        }

        using (var target = bitmap.Lock())
        {
            for (var row = 0; row < frame.Size.Height; row++)
            {
                var destination = IntPtr.Add(target.Address, row * target.RowBytes);
                if (frame.Bgra is null)
                {
                    Marshal.Copy(blackRow, 0, destination, sourceRowBytes);
                }
                else
                {
                    Marshal.Copy(frame.Bgra, row * sourceRowBytes, destination, sourceRowBytes);
                }
            }
        }

        nextBuffer = 1 - nextBuffer;
        return bitmap;
    }
}
