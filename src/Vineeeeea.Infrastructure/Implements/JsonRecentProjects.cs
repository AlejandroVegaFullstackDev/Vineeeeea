using System.Text.Json;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;

namespace Vineeeeea.Infrastructure.Implements;

/// <summary>
/// The recent list is a convenience, never the user's work: if its file is missing or unreadable the start
/// screen simply shows no recents instead of failing.
/// </summary>
public sealed class JsonRecentProjects(AppConfig config) : IRecentProjects
{
    private const int MaxRecentProjects = 8;
    private const string PartialSuffix = ".part";

    public IReadOnlyList<RecentProject> List() =>
        Read().Where(project => File.Exists(project.FilePath)).ToList();

    public void Remember(RecentProject project)
    {
        var updated = Read()
            .Where(existing => !string.Equals(existing.FilePath, project.FilePath, StringComparison.OrdinalIgnoreCase))
            .Prepend(project)
            .Take(MaxRecentProjects)
            .ToList();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(config.RecentProjectsFile)!);
            var partialPath = config.RecentProjectsFile + PartialSuffix;
            File.WriteAllText(partialPath, JsonSerializer.Serialize(updated));
            File.Move(partialPath, config.RecentProjectsFile, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private List<RecentProject> Read()
    {
        try
        {
            return File.Exists(config.RecentProjectsFile)
                ? JsonSerializer.Deserialize<List<RecentProject>>(File.ReadAllText(config.RecentProjectsFile)) ?? []
                : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
