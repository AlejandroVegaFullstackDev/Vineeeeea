using System.Text.Json;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Infrastructure.Dtos;
using Vineeeeea.Infrastructure.Services;
using Vineeeeea.Infrastructure.Utils;
using static Vineeeeea.Infrastructure.Utils.FfmpegExpressions;

namespace Vineeeeea.Infrastructure.Implements;

public sealed class WhisperTranscriber(ProcessRunner processRunner, AppConfig config) : ISpeechTranscriber
{
    private const string WorkFolderName = "transcription";
    private const string AudioFileName = "audio.wav";
    private const string ResultBaseName = "result";
    private const string ResultExtension = ".json";
    private const string WhisperSampleRate = "16000";
    private const long MicrosecondsPerMillisecond = 1000;

    public async Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(
        string mediaPath,
        long sourceStartUs,
        long sourceDurationUs,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(config.WhisperPath) || !File.Exists(config.WhisperModelPath))
        {
            throw new ExternalServiceException("No se encontró whisper.cpp o su modelo; revisa VINEEEEEA_WHISPER_PATH y VINEEEEEA_WHISPER_MODEL.");
        }

        if (!File.Exists(mediaPath))
        {
            throw new NotFoundException($"Falta el medio {Path.GetFileName(mediaPath)}.");
        }

        var workDirectory = Path.Combine(config.CacheDirectory, WorkFolderName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        try
        {
            var audioPath = Path.Combine(workDirectory, AudioFileName);
            await RunAsync(config.FfmpegPath,
            [
                "-v", "error", "-nostdin", "-y",
                "-ss", Seconds(sourceStartUs),
                "-t", Seconds(sourceDurationUs),
                "-i", FfmpegConventions.LocalFileProtocol + mediaPath,
                "-vn", "-ac", "1", "-ar", WhisperSampleRate, "-c:a", "pcm_s16le",
                audioPath,
            ], "ffmpeg no pudo extraer el audio del clip.", cancellationToken);

            var resultBase = Path.Combine(workDirectory, ResultBaseName);
            await RunAsync(config.WhisperPath,
            [
                "-m", config.WhisperModelPath,
                "-f", audioPath,
                "-l", config.CaptionLanguage,
                "-oj",
                "-of", resultBase,
                "-np",
            ], "whisper.cpp no pudo transcribir el audio.", cancellationToken);

            return Parse(await File.ReadAllTextAsync(resultBase + ResultExtension, cancellationToken));
        }
        finally
        {
            Directory.Delete(workDirectory, recursive: true);
        }
    }

    internal static IReadOnlyList<TranscriptSegment> Parse(string json)
    {
        WhisperOutputDto? output;
        try
        {
            output = JsonSerializer.Deserialize<WhisperOutputDto>(json);
        }
        catch (JsonException)
        {
            throw new ExternalServiceException("whisper.cpp devolvió una transcripción que no se pudo interpretar.");
        }

        return (output?.Transcription ?? [])
            .Where(segment => segment.Offsets is not null && !string.IsNullOrWhiteSpace(segment.Text))
            .Select(segment => new TranscriptSegment(
                segment.Offsets!.From * MicrosecondsPerMillisecond,
                segment.Offsets.To * MicrosecondsPerMillisecond,
                segment.Text!.Trim()))
            .ToList();
    }

    private async Task RunAsync(string executable, string[] arguments, string failureMessage, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(executable, arguments, config.TranscriptionTimeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new ExternalServiceException(failureMessage);
        }
    }
}
