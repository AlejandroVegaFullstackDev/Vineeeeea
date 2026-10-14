using Vineeeeea.Application.Services;

namespace Vineeeeea.Application.Controllers;

public sealed class HistoryController(EditorSession session)
{
    public bool CanUndo => session.CanUndo;
    public bool CanRedo => session.CanRedo;

    public void Undo() => session.Undo();

    public void Redo() => session.Redo();
}
