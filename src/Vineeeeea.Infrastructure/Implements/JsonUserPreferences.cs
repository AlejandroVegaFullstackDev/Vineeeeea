using System.Text.Json;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Services;
using Vineeeeea.Infrastructure.Dtos;

namespace Vineeeeea.Infrastructure.Implements;

/// <summary>
/// User preferences are a convenience, not the user's work: a missing or unreadable file reads as defaults
/// and a failed write is swallowed, so nothing ever blocks editing.
/// </summary>
public sealed class JsonUserPreferences(AppConfig config) : IUserPreferences
{
    private const string PartialSuffix = ".part";

    private PreferencesDto current = Read(config);

    public bool TimelineTipDismissed => current.TimelineTipDismissed;

    public void DismissTimelineTip()
    {
        if (current.TimelineTipDismissed)
        {
            return;
        }

        current = current with { TimelineTipDismissed = true };
        Write(config, current);
    }

    private static PreferencesDto Read(AppConfig config)
    {
        try
        {
            return File.Exists(config.PreferencesFile)
                ? JsonSerializer.Deserialize<PreferencesDto>(File.ReadAllText(config.PreferencesFile)) ?? new PreferencesDto()
                : new PreferencesDto();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new PreferencesDto();
        }
    }

    private static void Write(AppConfig config, PreferencesDto preferences)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(config.PreferencesFile)!);
            var partialPath = config.PreferencesFile + PartialSuffix;
            File.WriteAllText(partialPath, JsonSerializer.Serialize(preferences));
            File.Move(partialPath, config.PreferencesFile, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
