using Avalonia.Headless.XUnit;
using Vineeeeea.App.Utils;

namespace Vineeeeea.App.Tests;

public class ProjectFormatTests
{
    [AvaloniaFact]
    public void Choosing_a_vertical_format_and_sixty_fps_updates_the_project()
    {
        using var harness = EditorHarness.Open();
        harness.ViewModel.NewProjectCommand.Execute(null);

        harness.ViewModel.Format.SelectedCanvas = FormatPresets.Canvases.Single(preset => preset.Width == 1080 && preset.Height == 1920);
        harness.ViewModel.Format.SelectedFrameRate = FormatPresets.FrameRates.Single(preset => preset.FrameRate.Numerator == 60);

        var settings = harness.Session.RequireProject().Settings;
        Assert.Equal((1080, 1920, 60), (settings.Width, settings.Height, settings.FrameRate.Numerator));
        Assert.Contains("1080×1920", harness.ViewModel.ProjectSummary);
    }
}
