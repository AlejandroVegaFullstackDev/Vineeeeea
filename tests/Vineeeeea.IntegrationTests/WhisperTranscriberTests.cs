using System.Diagnostics;
using Vineeeeea.Application.Config;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Infrastructure.Implements;
using Vineeeeea.Infrastructure.Services;

namespace Vineeeeea.IntegrationTests;

public class WhisperTranscriberTests
{
    private const string SpokenSentence = "Hola a todos. Bienvenidos a mi canal de video.";

    [Fact]
    public void Parses_whisper_segments_into_microseconds()
    {
        const string json = """
            { "transcription": [
                { "offsets": { "from": 0, "to": 1740 }, "text": " Hola a todos." },
                { "offsets": { "from": 1740, "to": 4260 }, "text": " " },
                { "offsets": { "from": 4260, "to": 6240 }, "text": " Adiós." }
            ] }
            """;

        var segments = WhisperTranscriber.Parse(json);

        Assert.Equal(2, segments.Count);
        Assert.Equal((0L, 1_740_000L, "Hola a todos."), (segments[0].StartUs, segments[0].EndUs, segments[0].Text));
    }

    [Fact]
    public void Rejects_unreadable_whisper_output()
    {
        Assert.Throws<ExternalServiceException>(() => WhisperTranscriber.Parse("no es json"));
    }

    [Fact]
    [Trait("Category", "Whisper")]
    public async Task Transcribes_spanish_speech_from_part_of_a_file()
    {
        using var directory = new TemporaryDirectory();
        var speechPath = directory.Combine("voz en español.wav");
        await SynthesizeSpanishAsync(speechPath);
        var config = AppConfig.FromEnvironment(Environment.GetEnvironmentVariable) with { CacheDirectory = directory.Combine("cache") };

        var segments = await new WhisperTranscriber(new ProcessRunner(), config).TranscribeAsync(speechPath, 0, 10_000_000, CancellationToken.None);

        Assert.NotEmpty(segments);
        Assert.Contains("hola", string.Join(' ', segments.Select(segment => segment.Text)), StringComparison.OrdinalIgnoreCase);
        Assert.All(segments, segment => Assert.True(segment.EndUs > segment.StartUs));
        Assert.False(Directory.Exists(Path.Combine(config.CacheDirectory, "transcription")) &&
                     Directory.EnumerateFileSystemEntries(Path.Combine(config.CacheDirectory, "transcription")).Any());
    }

    private static async Task SynthesizeSpanishAsync(string outputPath)
    {
        var script = string.Concat(
            "Add-Type -AssemblyName System.Speech;",
            "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer;",
            "$voz = $s.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.TwoLetterISOLanguageName -eq 'es' } | Select-Object -First 1;",
            "if ($voz) { $s.SelectVoice($voz.VoiceInfo.Name) };",
            "$s.SetOutputToWaveFile($env:VINEEEEEA_TEST_SPEECH_FILE);",
            "$s.Speak($env:VINEEEEEA_TEST_SPEECH_TEXT);",
            "$s.Dispose()");
        var startInfo = new ProcessStartInfo("powershell") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-NoProfile", "-Command", script })
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["VINEEEEEA_TEST_SPEECH_FILE"] = outputPath;
        startInfo.Environment["VINEEEEEA_TEST_SPEECH_TEXT"] = SpokenSentence;

        using var process = Process.Start(startInfo)!;
        var errors = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0 && File.Exists(outputPath), errors);
    }
}
