using System.Diagnostics;

namespace Vineeeeea.IntegrationTests;

internal static class TestMedia
{
    public static Task GenerateClipAsync(string outputPath) => RunFfmpegAsync(
        "-f", "lavfi", "-i", "testsrc=duration=2:size=320x240:rate=30",
        "-f", "lavfi", "-i", "sine=frequency=440:duration=2",
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest",
        outputPath);

    public static Task GenerateVerticalClipAsync(string outputPath) => RunFfmpegAsync(
        "-f", "lavfi", "-i", "testsrc=duration=1:size=180x320:rate=30",
        "-c:v", "libx264", "-pix_fmt", "yuv420p",
        outputPath);

    public static Task GenerateRedImageAsync(string outputPath) => RunFfmpegAsync(
        "-f", "lavfi", "-i", "color=c=red:s=200x100",
        "-frames:v", "1",
        outputPath);

    /// <summary>A one-red-then-one-blue clip, so reversing the segment is visible in the first frame.</summary>
    public static Task GenerateRedThenBlueClipAsync(string outputPath) => RunFfmpegAsync(
        "-f", "lavfi", "-i", "color=c=red:s=320x240:r=30:d=1",
        "-f", "lavfi", "-i", "color=c=blue:s=320x240:r=30:d=1",
        "-filter_complex", "[0:v][1:v]concat=n=2:v=1:a=0",
        "-c:v", "libx264", "-pix_fmt", "yuv420p",
        outputPath);

    /// <summary>A full-canvas image with a red square near a corner, so zooming it out of frame is visible.</summary>
    public static Task GenerateCornerSquareImageAsync(string outputPath) => RunFfmpegAsync(
        "-f", "lavfi", "-i", "color=c=black:s=320x240",
        "-vf", "drawbox=x=10:y=10:w=40:h=40:color=red:t=fill",
        "-frames:v", "1",
        outputPath);

    private static async Task RunFfmpegAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "-v", "error", "-y" }.Concat(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var errors = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, errors);
    }
}
