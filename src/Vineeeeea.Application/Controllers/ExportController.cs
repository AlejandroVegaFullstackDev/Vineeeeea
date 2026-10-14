using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Controllers;

public sealed class ExportController(EditorSession session, IRenderEngine renderEngine)
{
    public Task ExportAsync(ExportDestination destination, string outputPath, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var exportPath = FileConventions.RequireExportPath(outputPath);
        var project = session.RequireProject();
        var endFrame = project.EndFrame();
        if (endFrame == 0)
        {
            throw new BusinessRuleException("Tu video está vacío; agrega algo antes de exportar.");
        }

        var outputSize = new FrameSize(destination.Width, destination.Height);
        return renderEngine.ExportAsync(
            RenderPlanBuilder.Build(project, 0, endFrame, outputSize), exportPath, destination.Encoding, progress, cancellationToken);
    }

    /// <summary>True when the project's shape differs from the destination, so the video gets bars around it.</summary>
    public bool WillLetterbox(ExportDestination destination)
    {
        var settings = session.RequireProject().Settings;
        return settings.Width * destination.Height != destination.Width * settings.Height;
    }
}
