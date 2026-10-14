using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vineeeeea.App.ViewModels;
using Vineeeeea.App.Views;
using Vineeeeea.Domain.Utils;
using Vineeeeea.IntegrationTests;

namespace Vineeeeea.App.Tests;

public class EasyEditorTests
{
    private static async Task<Guid> StartWithClipAsync(EditorHarness harness)
    {
        var clipPath = harness.Directory.Combine("paisaje.mp4");
        await TestMedia.GenerateClipAsync(clipPath);
        await harness.ViewModel.ImportFilesAndAppendAsync([clipPath]);
        var clip = harness.Session.RequireProject().Tracks.SelectMany(track => track.Clips).Single();
        harness.ViewModel.SelectClip(clip.Id);
        await harness.SettleAsync();
        return clip.Id;
    }

    [AvaloniaFact]
    public async Task The_easy_editor_is_the_default_shell_when_a_project_is_open()
    {
        using var harness = EditorHarness.Open();
        await StartWithClipAsync(harness);

        Assert.True(harness.ViewModel.IsEasyEditing);
        Assert.True(harness.Window.GetVisualDescendants().OfType<EasyEditorView>().Single().IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Switching_to_advanced_mode_and_back_keeps_the_selection_and_the_playhead()
    {
        using var harness = EditorHarness.Open();
        var clipId = await StartWithClipAsync(harness);
        harness.ViewModel.Player.PlayheadPosition = 20;

        harness.ViewModel.ToggleAdvancedModeCommand.Execute(null);
        Assert.True(harness.ViewModel.IsAdvancedEditing);

        harness.ViewModel.ToggleAdvancedModeCommand.Execute(null);
        Assert.True(harness.ViewModel.IsEasyEditing);
        Assert.Equal(clipId, harness.ViewModel.SelectedClipId);
        Assert.Equal(20, harness.ViewModel.Player.PlayheadFrame);
    }

    [AvaloniaFact]
    public async Task The_rotate_tile_turns_the_selected_clip_and_can_be_undone()
    {
        using var harness = EditorHarness.Open();
        var clipId = await StartWithClipAsync(harness);

        harness.ViewModel.ClipTools.RotateCommand.Execute(null);
        Assert.Equal(90, harness.Session.RequireProject().GetClip(clipId).FindEffect(EffectCatalog.Rotate)!.Parameters[EffectCatalog.Degrees]);

        harness.ViewModel.UndoCommand.Execute(null);
        Assert.Null(harness.Session.RequireProject().GetClip(clipId).FindEffect(EffectCatalog.Rotate));
        await harness.SettleAsync();
    }

    [AvaloniaFact]
    public async Task A_style_tile_applies_a_color_look_as_an_effect()
    {
        using var harness = EditorHarness.Open();
        var clipId = await StartWithClipAsync(harness);
        var vivid = harness.ViewModel.ClipTools.Styles.Single(style => style.Title == "Vivo");

        harness.ViewModel.ClipTools.ApplyStyleCommand.Execute(vivid);

        Assert.Contains(harness.Session.RequireProject().GetClip(clipId).Effects,
            effect => effect.Type == EffectCatalog.Saturation);
        await harness.SettleAsync();
    }

    [AvaloniaFact]
    public async Task The_flip_tile_mirrors_the_clip_and_toggles_back()
    {
        using var harness = EditorHarness.Open();
        var clipId = await StartWithClipAsync(harness);
        var tools = harness.ViewModel.ClipTools;

        tools.FlipHorizontalCommand.Execute(null);
        Assert.Equal(1, harness.Session.RequireProject().GetClip(clipId).FindEffect(EffectCatalog.Flip)!.Parameters[EffectCatalog.Horizontal]);

        tools.FlipHorizontalCommand.Execute(null);
        Assert.Equal(0, harness.Session.RequireProject().GetClip(clipId).FindEffect(EffectCatalog.Flip)!.Parameters[EffectCatalog.Horizontal]);
        await harness.SettleAsync();
    }

    [AvaloniaFact]
    public async Task The_green_screen_tile_adds_and_removes_the_chroma_key()
    {
        using var harness = EditorHarness.Open();
        var clipId = await StartWithClipAsync(harness);
        var tools = harness.ViewModel.ClipTools;

        tools.ToggleGreenScreenCommand.Execute(null);
        Assert.NotNull(harness.Session.RequireProject().GetClip(clipId).FindEffect(EffectCatalog.ChromaKey));

        tools.ToggleGreenScreenCommand.Execute(null);
        Assert.Null(harness.Session.RequireProject().GetClip(clipId).FindEffect(EffectCatalog.ChromaKey));
        await harness.SettleAsync();
    }

    [AvaloniaFact]
    public async Task The_clean_voice_toggle_adds_denoise_and_normalize_together()
    {
        using var harness = EditorHarness.Open();
        var clipId = await StartWithClipAsync(harness);
        var tools = harness.ViewModel.ClipTools;

        tools.ToggleCleanAudioCommand.Execute(null);
        var clip = harness.Session.RequireProject().GetClip(clipId);
        Assert.NotNull(clip.FindEffect(EffectCatalog.Denoise));
        Assert.NotNull(clip.FindEffect(EffectCatalog.Normalize));

        tools.ToggleCleanAudioCommand.Execute(null);
        clip = harness.Session.RequireProject().GetClip(clipId);
        Assert.Null(clip.FindEffect(EffectCatalog.Denoise));
        Assert.Null(clip.FindEffect(EffectCatalog.Normalize));
        await harness.SettleAsync();
    }

    [AvaloniaFact]
    public async Task The_add_column_lists_only_the_media_of_the_chosen_category()
    {
        using var harness = EditorHarness.Open();
        await StartWithClipAsync(harness);
        var addMedia = harness.ViewModel.AddMedia;

        addMedia.ChooseVideosAndPhotosCommand.Execute(null);
        Assert.True(addMedia.HasMedia);

        addMedia.ChooseMusicAndSoundCommand.Execute(null);
        Assert.False(addMedia.HasMedia);
    }

    [AvaloniaFact]
    public async Task The_base_video_lane_is_named_Tu_video()
    {
        using var harness = EditorHarness.Open();
        await StartWithClipAsync(harness);

        var lane = harness.ViewModel.Tracks.Single();
        Assert.Equal("Tu video", lane.EasyName);
    }

    [AvaloniaFact]
    [Trait("Category", "Screenshots")]
    public async Task Captures_the_easy_editor()
    {
        var outputDirectory = Environment.GetEnvironmentVariable("VINEEEEEA_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        using var harness = EditorHarness.Open();
        await StartWithClipAsync(harness);
        harness.ViewModel.Player.PlayheadPosition = 20;
        await harness.WaitUntilAsync(() => harness.ViewModel.Player.Preview is not null);
        await Task.Delay(1200);

        using var frame = harness.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(outputDirectory, "40-editor-facil.png"), PngBitmapEncoderOptions.Default);
    }
}
