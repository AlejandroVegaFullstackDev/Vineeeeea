using Avalonia.Headless.XUnit;

namespace Vineeeeea.App.Tests;

public class TextEditingTests
{
    [AvaloniaFact]
    public void Adding_a_text_selects_it_and_typing_in_the_inspector_changes_it()
    {
        using var harness = EditorHarness.Open();
        harness.ViewModel.NewProjectCommand.Execute(null);

        harness.ViewModel.AddTextCommand.Execute(null);
        harness.ViewModel.Inspector.TextContent = "¡Suscríbete!";
        harness.ViewModel.Inspector.TextHasBackground = true;

        var project = harness.Session.RequireProject();
        var clip = project.GetClip(harness.ViewModel.SelectedClipId!.Value);
        var text = project.GetAsset(clip.AssetId).Text!;
        Assert.True(harness.ViewModel.Inspector.IsTextClip);
        Assert.Equal(("¡Suscríbete!", true), (text.Content, text.HasBackground));
        Assert.Empty(harness.ViewModel.Assets);
    }
}
