using Vineeeeea.Application.Services;

namespace Vineeeeea.Application.Controllers;

public sealed class MediaLibraryController(EditorSession session)
{
    public void RemoveAsset(Guid assetId) => session.Apply(project => project.RemoveAsset(assetId));
}
