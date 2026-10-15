using System.Globalization;
using System.Text.RegularExpressions;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Config;

public sealed partial record AppConfig(
    string FfprobePath,
    string FfmpegPath,
    string FfplayPath,
    string CacheDirectory,
    string RecentProjectsFile,
    string PreferencesFile,
    string FontPath,
    string BoldFontPath,
    string WhisperPath,
    string WhisperModelPath,
    string CaptionLanguage,
    TimeSpan ProbeTimeout,
    TimeSpan MediaPreviewTimeout,
    TimeSpan TranscriptionTimeout,
    int PreviewMaxSide,
    ExportEncoding ExportEncoding,
    ProjectSettings DefaultProjectSettings)
{
    private const string FfprobePathVariable = "VINEEEEEA_FFPROBE_PATH";
    private const string FfmpegPathVariable = "VINEEEEEA_FFMPEG_PATH";
    private const string FfplayPathVariable = "VINEEEEEA_FFPLAY_PATH";
    private const string CacheDirectoryVariable = "VINEEEEEA_CACHE_DIR";
    private const string FontPathVariable = "VINEEEEEA_FONT_PATH";
    private const string BoldFontPathVariable = "VINEEEEEA_BOLD_FONT_PATH";
    private const string WhisperPathVariable = "VINEEEEEA_WHISPER_PATH";
    private const string WhisperModelVariable = "VINEEEEEA_WHISPER_MODEL";
    private const string CaptionLanguageVariable = "VINEEEEEA_CAPTION_LANGUAGE";
    private const string ProbeTimeoutVariable = "VINEEEEEA_PROBE_TIMEOUT_SECONDS";
    private const string DefaultFfprobePath = "ffprobe";
    private const string DefaultFfmpegPath = "ffmpeg";
    private const string DefaultFfplayPath = "ffplay";
    private const string AppDataFolderName = "Vineeeeea";
    private const string CacheFolderName = "Cache";
    private const string RecentProjectsFileName = "recientes.json";
    private const string PreferencesFileName = "preferencias.json";
    private const string ToolsFolderName = "tools";
    private static readonly string DefaultWhisperRelativePath = OperatingSystem.IsWindows()
        ? Path.Combine("whisper", "Release", "whisper-cli.exe")
        : Path.Combine("whisper", "whisper-cli");
    private const string DefaultWhisperModelRelativePath = @"models\ggml-base.bin";
    private const string DefaultCaptionLanguage = "auto";
    private const int DefaultProbeTimeoutSeconds = 30;
    private const int DefaultMediaPreviewTimeoutSeconds = 120;
    private const int DefaultTranscriptionTimeoutMinutes = 30;
    private const int DefaultPreviewMaxSide = 640;
    private const int DefaultWidth = 1920;
    private const int DefaultHeight = 1080;
    private const int DefaultFrameRateNumerator = 30;
    private const int DefaultFrameRateDenominator = 1;
    private const int DefaultAudioSampleRate = 48_000;
    private const int DefaultExportCrf = 20;
    private const string DefaultExportPreset = "medium";
    private const int DefaultExportAudioBitrateKbps = 192;

    public static AppConfig FromEnvironment(Func<string, string?> readVariable)
    {
        var timeoutText = readVariable(ProbeTimeoutVariable);
        var timeoutSeconds = DefaultProbeTimeoutSeconds;
        if (!string.IsNullOrWhiteSpace(timeoutText)
            && (!int.TryParse(timeoutText, NumberStyles.None, CultureInfo.InvariantCulture, out timeoutSeconds) || timeoutSeconds <= 0))
        {
            throw new ValidationException($"{ProbeTimeoutVariable} debe ser un número entero positivo de segundos.");
        }

        var captionLanguage = ReadValue(readVariable, CaptionLanguageVariable, DefaultCaptionLanguage);
        if (!LanguageCode().IsMatch(captionLanguage))
        {
            throw new ValidationException($"{CaptionLanguageVariable} debe ser «auto» o un código de idioma como «es».");
        }

        var appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppDataFolderName);
        var toolsDirectory = Path.Combine(appDataDirectory, ToolsFolderName);

        return new AppConfig(
            ReadValue(readVariable, FfprobePathVariable, DefaultFfprobePath),
            ReadValue(readVariable, FfmpegPathVariable, DefaultFfmpegPath),
            ReadValue(readVariable, FfplayPathVariable, DefaultFfplayPath),
            ReadValue(readVariable, CacheDirectoryVariable, Path.Combine(appDataDirectory, CacheFolderName)),
            Path.Combine(appDataDirectory, RecentProjectsFileName),
            Path.Combine(appDataDirectory, PreferencesFileName),
            ReadValue(readVariable, FontPathVariable, FindSystemFont(bold: false)),
            ReadValue(readVariable, BoldFontPathVariable, FindSystemFont(bold: true)),
            ReadValue(readVariable, WhisperPathVariable, Path.Combine(toolsDirectory, DefaultWhisperRelativePath)),
            ReadValue(readVariable, WhisperModelVariable, Path.Combine(toolsDirectory, DefaultWhisperModelRelativePath)),
            captionLanguage,
            TimeSpan.FromSeconds(timeoutSeconds),
            TimeSpan.FromSeconds(DefaultMediaPreviewTimeoutSeconds),
            TimeSpan.FromMinutes(DefaultTranscriptionTimeoutMinutes),
            DefaultPreviewMaxSide,
            new ExportEncoding(DefaultExportCrf, DefaultExportPreset, DefaultExportAudioBitrateKbps),
            new ProjectSettings(
                DefaultWidth,
                DefaultHeight,
                new FrameRate(DefaultFrameRateNumerator, DefaultFrameRateDenominator),
                DefaultAudioSampleRate));
    }

    private static string ReadValue(Func<string, string?> readVariable, string variable, string fallback)
    {
        var value = readVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// <summary>Segoe UI on Windows; on Linux/macOS the first common sans font found, so drawtext keeps working.</summary>
    private static string FindSystemFont(bool bold)
    {
        var fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string[] candidates = OperatingSystem.IsWindows()
            ? [Path.Combine(fontsDirectory, bold ? "segoeuib.ttf" : "segoeui.ttf")]
            :
            [
                bold ? "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" : "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                bold ? "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf" : "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
                bold ? "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf" : "/usr/share/fonts/TTF/DejaVuSans.ttf",
                bold ? "/System/Library/Fonts/Supplemental/Arial Bold.ttf" : "/System/Library/Fonts/Supplemental/Arial.ttf",
            ];
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    [GeneratedRegex("^(auto|[a-z]{2,3})$")]
    private static partial Regex LanguageCode();
}

public sealed record ExportEncoding(int VideoCrf, string VideoPreset, int AudioBitrateKbps);
