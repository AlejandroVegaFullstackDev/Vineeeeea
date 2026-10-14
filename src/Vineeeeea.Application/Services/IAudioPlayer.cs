using Vineeeeea.Application.Dtos;

namespace Vineeeeea.Application.Services;

public interface IAudioPlayer
{
    Task PlayAsync(RenderPlan plan, CancellationToken cancellationToken);
}
