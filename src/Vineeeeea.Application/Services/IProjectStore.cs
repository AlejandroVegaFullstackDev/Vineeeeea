using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Services;

public interface IProjectStore
{
    Task SaveAsync(Project project, string filePath, CancellationToken cancellationToken);

    Task<Project> LoadAsync(string filePath, CancellationToken cancellationToken);
}
