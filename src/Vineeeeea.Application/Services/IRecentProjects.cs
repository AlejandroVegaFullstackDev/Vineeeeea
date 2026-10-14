using Vineeeeea.Application.Dtos;

namespace Vineeeeea.Application.Services;

public interface IRecentProjects
{
    IReadOnlyList<RecentProject> List();

    void Remember(RecentProject project);
}
