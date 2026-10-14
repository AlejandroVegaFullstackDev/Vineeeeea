using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Infrastructure.Implements;

namespace Vineeeeea.IntegrationTests;

public class JsonRecentProjectsTests
{
    [Fact]
    public void Keeps_the_latest_first_without_duplicates_up_to_eight_existing_projects()
    {
        using var directory = new TemporaryDirectory();
        var recents = new JsonRecentProjects(ConfigIn(directory));
        var paths = Enumerable.Range(1, 10).Select(index => directory.Combine($"proyecto {index}.vine")).ToList();
        paths.ForEach(path => File.WriteAllText(path, "{}"));

        foreach (var path in paths)
        {
            recents.Remember(Recent(path));
        }

        recents.Remember(Recent(paths[9]));
        File.Delete(paths[8]);

        var listed = recents.List().Select(project => project.FilePath).ToList();
        Assert.Equal(paths[9], listed[0]);
        Assert.Equal(7, listed.Count);
        Assert.DoesNotContain(paths[8], listed);
        Assert.Equal(listed.Count, listed.Distinct().Count());
    }

    [Fact]
    public void A_damaged_list_reads_as_empty_and_is_replaced_on_the_next_save()
    {
        using var directory = new TemporaryDirectory();
        var config = ConfigIn(directory);
        File.WriteAllText(config.RecentProjectsFile, "{ esto no es json");
        var projectPath = directory.Combine("demo.vine");
        File.WriteAllText(projectPath, "{}");
        var recents = new JsonRecentProjects(config);

        Assert.Empty(recents.List());
        recents.Remember(Recent(projectPath));
        Assert.Single(recents.List());
    }

    private static AppConfig ConfigIn(TemporaryDirectory directory) =>
        AppConfig.FromEnvironment(_ => null) with { RecentProjectsFile = directory.Combine("recientes.json") };

    private static RecentProject Recent(string path) =>
        new(path, Path.GetFileNameWithoutExtension(path), 8_000_000, PreviewMediaPath: null, DateTimeOffset.UtcNow);
}
