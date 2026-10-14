using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;

namespace Vineeeeea.Application.Services;

public interface IRenderEngine
{
    Task StreamVideoAsync(
        RenderPlan plan,
        Func<byte[], CancellationToken, ValueTask> onFrame,
        CancellationToken cancellationToken);

    Task ExportAsync(
        RenderPlan plan,
        string outputPath,
        ExportEncoding encoding,
        IProgress<double> progress,
        CancellationToken cancellationToken);
}
