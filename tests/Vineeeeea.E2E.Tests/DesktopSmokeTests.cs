using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace Vineeeeea.E2E.Tests;

/// <summary>
/// Drives the packaged windows executable through Windows UI Automation, the desktop equivalent of a browser E2E.
/// It only runs when VINEEEEEA_APP_EXE points to a built Vineeeeea.Desktop.exe, so it stays off machines without a desktop.
/// </summary>
public class DesktopSmokeTests
{
    private const string ExeVariable = "VINEEEEEA_APP_EXE";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "E2E")]
    public void The_packaged_app_starts_a_project_and_reaches_the_editor()
    {
        if (Environment.GetEnvironmentVariable(ExeVariable) is not { Length: > 0 } exe || !File.Exists(exe))
        {
            return;
        }

        using var automation = new UIA3Automation();
        using var app = Application.Launch(exe);
        var window = app.GetMainWindow(automation, Timeout);
        Assert.NotNull(window);
        Assert.Contains("Vineeeeea", window!.Title);

        Assert.NotNull(window.FindFirstDescendant(condition => condition.ByName("Forma del video")));
        Assert.NotNull(window.FindFirstDescendant(condition => condition.ByName("Horizontal")));

        var startButton = window.FindAllDescendants(condition => condition.ByControlType(ControlType.Button))
            .FirstOrDefault(button => button.Name.Contains("Empezar", StringComparison.Ordinal));
        Assert.NotNull(startButton);
        startButton!.AsButton().Invoke();

        var editorReady = WaitUntil(() => window.Title.Contains("Mi video", StringComparison.Ordinal));
        Assert.True(editorReady, $"El editor no se abrió tras crear el proyecto. Título: {window.Title}");

        app.Close();
    }

    private static bool WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }
}
