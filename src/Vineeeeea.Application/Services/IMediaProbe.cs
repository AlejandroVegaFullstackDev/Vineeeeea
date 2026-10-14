using Vineeeeea.Application.Dtos;

namespace Vineeeeea.Application.Services;

public interface IMediaProbe
{
    Task<ProbedMedia> ProbeAsync(string filePath, CancellationToken cancellationToken);
}
