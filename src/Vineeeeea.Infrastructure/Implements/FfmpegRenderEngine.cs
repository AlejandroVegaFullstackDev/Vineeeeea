using System.Globalization;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Infrastructure.Dtos;
using Vineeeeea.Infrastructure.Services;
using static Vineeeeea.Infrastructure.Utils.FfmpegExpressions;

namespace Vineeeeea.Infrastructure.Implements;

public sealed class FfmpegRenderEngine(ProcessRunner processRunner, TextFileStore textFiles, AppConfig config) : IRenderEngine
{
    private const string ProgressTimeKey = "out_time_us=";
    private const string PartialFileSuffix = ".part";

    public async Task StreamVideoAsync(
        RenderPlan plan,
        Func<byte[], CancellationToken, ValueTask> onFrame,
        CancellationToken cancellationToken)
    {
        var graph = FfmpegGraphBuilder.Build(plan, includeVideo: true, includeAudio: false, TextRendering());
        List<string> arguments = ["-v", "error", "-nostdin", .. graph.InputArguments];
        arguments.AddRange(
        [
            "-filter_complex", graph.FilterComplex,
            "-map", FfmpegGraphBuilder.VideoOutput,
            "-an",
            "-frames:v", Integer(plan.DurationFrames),
            "-pix_fmt", "bgra",
            "-f", "rawvideo",
            "pipe:1",
        ]);

        async Task ConsumeFramesAsync(Stream output, CancellationToken token)
        {
            for (long delivered = 0; delivered < plan.DurationFrames; delivered++)
            {
                var pixels = new byte[plan.OutputSize.FrameBytes];
                var read = await output.ReadAtLeastAsync(pixels, pixels.Length, throwOnEndOfStream: false, token);
                if (read < pixels.Length)
                {
                    return;
                }

                await onFrame(pixels, token);
            }
        }

        var exitCode = await processRunner.RunStreamingAsync(config.FfmpegPath, arguments, ConsumeFramesAsync, cancellationToken);
        if (exitCode != 0)
        {
            throw new ExternalServiceException("ffmpeg no pudo renderizar la previsualización.");
        }
    }

    public async Task ExportAsync(RenderPlan plan, string outputPath, ExportEncoding encoding, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var graph = FfmpegGraphBuilder.Build(plan, includeVideo: true, includeAudio: true, TextRendering());
        var partialPath = outputPath + PartialFileSuffix;
        List<string> arguments = ["-y", "-v", "error", "-nostdin", .. graph.InputArguments];
        arguments.AddRange(
        [
            "-filter_complex", graph.FilterComplex,
            "-map", FfmpegGraphBuilder.VideoOutput,
            "-map", FfmpegGraphBuilder.AudioOutput,
            "-c:v", "libx264",
            "-preset", encoding.VideoPreset,
            "-crf", Integer(encoding.VideoCrf),
            "-pix_fmt", "yuv420p",
            "-r", Rate(plan.FrameRate),
            "-c:a", "aac",
            "-b:a", string.Concat(Integer(encoding.AudioBitrateKbps), "k"),
            "-movflags", "+faststart",
            "-progress", "pipe:1",
            "-nostats",
            "-f", "mp4",
            partialPath,
        ]);

        async Task ReportProgressAsync(Stream output, CancellationToken token)
        {
            using var reader = new StreamReader(output);
            while (await reader.ReadLineAsync(token) is { } line)
            {
                if (line.StartsWith(ProgressTimeKey, StringComparison.Ordinal)
                    && long.TryParse(line.AsSpan(ProgressTimeKey.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var renderedUs))
                {
                    progress.Report(Math.Clamp((double)renderedUs / plan.DurationUs, 0, 1));
                }
            }
        }

        try
        {
            var exitCode = await processRunner.RunStreamingAsync(config.FfmpegPath, arguments, ReportProgressAsync, cancellationToken);
            if (exitCode != 0)
            {
                throw new ExternalServiceException("ffmpeg no pudo exportar el video.");
            }

            File.Move(partialPath, outputPath, overwrite: true);
            progress.Report(1);
        }
        finally
        {
            File.Delete(partialPath);
        }
    }

    private TextRenderingContext TextRendering() => new(config.FontPath, config.BoldFontPath, textFiles.PathFor);
}
