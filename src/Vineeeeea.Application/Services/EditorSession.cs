using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Services;

public sealed class EditorSession
{
    private const int MaxHistoryDepth = 200;

    private readonly List<Project> undoHistory = [];
    private readonly List<Project> redoHistory = [];
    private Project? savedProject;
    private string? lastMergeKey;

    public Project? Project { get; private set; }
    public string? FilePath { get; private set; }

    public bool HasUnsavedChanges => Project is not null && !ReferenceEquals(Project, savedProject);
    public bool CanUndo => undoHistory.Count > 0;
    public bool CanRedo => redoHistory.Count > 0;

    public event EventHandler? Changed;

    public Project RequireProject() =>
        Project ?? throw new BusinessRuleException("No hay ningún proyecto abierto.");

    public void Open(Project project, string? filePath)
    {
        Project = project;
        FilePath = filePath;
        savedProject = filePath is null ? null : project;
        undoHistory.Clear();
        redoHistory.Clear();
        lastMergeKey = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Consecutive edits with the same <paramref name="mergeKey"/> share one undo step, so dragging a slider is undone at once.
    /// </summary>
    public void Apply(Func<Project, Project> edit, string? mergeKey = null)
    {
        var current = RequireProject();
        var updated = edit(current);
        if (mergeKey is null || mergeKey != lastMergeKey)
        {
            Push(undoHistory, current);
        }

        redoHistory.Clear();
        lastMergeKey = mergeKey;
        Project = updated;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo() => Step(undoHistory, redoHistory, "No hay nada que deshacer.");

    public void Redo() => Step(redoHistory, undoHistory, "No hay nada que rehacer.");

    public void MarkSaved(string filePath)
    {
        FilePath = filePath;
        savedProject = Project;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Step(List<Project> from, List<Project> to, string emptyMessage)
    {
        var current = RequireProject();
        if (from.Count == 0)
        {
            throw new BusinessRuleException(emptyMessage);
        }

        Push(to, current);
        Project = from[^1];
        from.RemoveAt(from.Count - 1);
        lastMergeKey = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void Push(List<Project> history, Project project)
    {
        history.Add(project);
        if (history.Count > MaxHistoryDepth)
        {
            history.RemoveAt(0);
        }
    }
}
