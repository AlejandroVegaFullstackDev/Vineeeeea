using Vineeeeea.Infrastructure.Dtos;

namespace Vineeeeea.Infrastructure.Services;

/// <summary>
/// Runs the external media tools (ffmpeg, ffprobe, ffplay, whisper) with command-line style arguments.
/// Desktop launches real processes; platforms that cannot (iOS, Android) provide an in-process implementation.
/// </summary>
public interface IToolRunner
{
    Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>Hands the raw standard output stream to <paramref name="consumeOutput"/>; the tool is stopped if the consumer stops early.</summary>
    Task<int> RunStreamingAsync(
        string executable,
        IReadOnlyList<string> arguments,
        Func<Stream, CancellationToken, Task> consumeOutput,
        CancellationToken cancellationToken);

    /// <summary>Pipes the producer's output into the consumer's input until the consumer finishes.</summary>
    Task RunPipelineAsync(
        string producerExecutable,
        IReadOnlyList<string> producerArguments,
        string consumerExecutable,
        IReadOnlyList<string> consumerArguments,
        CancellationToken cancellationToken);
}
