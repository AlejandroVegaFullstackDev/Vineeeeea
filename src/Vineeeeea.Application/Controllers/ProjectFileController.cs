using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Controllers;

public sealed class ProjectFileController(EditorSession session, IProjectStore store, IRecentProjects recentProjects, AppConfig config)
{
    public void CreateNew(string name) =>
        session.Open(new Project(Guid.NewGuid(), name, config.DefaultProjectSettings), filePath: null);

    public void CreateNew(string name, CanvasShape shape, FrameRate frameRate, int audioSampleRate)
    {
        var (width, height) = CanvasShapes.SizeOf(shape);
        session.Open(new Project(Guid.NewGuid(), name, new ProjectSettings(width, height, frameRate, audioSampleRate)), filePath: null);
    }

    public async Task OpenAsync(string filePath, CancellationToken cancellationToken)
    {
        var projectPath = FileConventions.RequireProjectPath(filePath);
        var project = await store.LoadAsync(projectPath, cancellationToken);
        session.Open(project, projectPath);
        Remember(project, projectPath);
    }

    public IReadOnlyList<RecentProject> RecentProjects() => recentProjects.List();

    public Task SaveAsync(CancellationToken cancellationToken) =>
        session.FilePath is null
            ? throw new BusinessRuleException("El proyecto aún no tiene un archivo; usa Guardar como.")
            : SaveAsAsync(session.FilePath, cancellationToken);

    public async Task SaveAsAsync(string filePath, CancellationToken cancellationToken)
    {
        var projectPath = FileConventions.RequireProjectPath(filePath);
        var project = session.RequireProject();
        await store.SaveAsync(project, projectPath, cancellationToken);
        session.MarkSaved(projectPath);
        Remember(project, projectPath);
    }

    private void Remember(Project project, string projectPath)
    {
        var previewMedia = project.Assets.FirstOrDefault(asset => asset.Kind is MediaKind.Video or MediaKind.Image);
        var duration = project.Settings.FrameRate.FramesToMicroseconds(project.EndFrame());
        recentProjects.Remember(new RecentProject(projectPath, project.Name, duration, previewMedia?.Path, DateTimeOffset.UtcNow));
    }
}
