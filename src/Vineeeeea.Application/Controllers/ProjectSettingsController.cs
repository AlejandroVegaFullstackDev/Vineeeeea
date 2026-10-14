using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Controllers;

public sealed class ProjectSettingsController(EditorSession session)
{
    public void SetCanvas(int width, int height)
    {
        var current = session.RequireProject().Settings;
        var settings = new ProjectSettings(width, height, current.FrameRate, current.AudioSampleRate);
        session.Apply(project => project.ChangeSettings(settings));
    }

    /// <summary>Gives the project the shape of a visual medium, so a video dropped on the start screen fills the canvas.</summary>
    public void MatchCanvasTo(Guid assetId)
    {
        var asset = session.RequireProject().GetAsset(assetId);
        if (asset is { Width: { } width, Height: { } height })
        {
            var (canvasWidth, canvasHeight) = CanvasShapes.SizeOf(CanvasShapes.ShapeOf(width, height));
            SetCanvas(canvasWidth, canvasHeight);
        }
    }

    public void SetFrameRate(FrameRate frameRate)
    {
        var current = session.RequireProject().Settings;
        var settings = new ProjectSettings(current.Width, current.Height, frameRate, current.AudioSampleRate);
        session.Apply(project => project.ChangeSettings(settings));
    }
}
