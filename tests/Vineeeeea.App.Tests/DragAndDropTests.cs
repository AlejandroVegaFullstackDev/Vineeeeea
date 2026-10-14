using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Vineeeeea.App.Utils;
using Vineeeeea.IntegrationTests;

namespace Vineeeeea.App.Tests;

public class DragAndDropTests
{
    [AvaloniaFact]
    public async Task Dropping_a_file_from_the_explorer_on_the_timeline_imports_and_places_it()
    {
        using var harness = EditorHarness.Open();
        var mediaPath = harness.Directory.Combine("mi video.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var data = await harness.FileDropAsync(mediaPath);
        var target = harness.CenterOf("TimelinePanel");

        harness.Window.DragDrop(target, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        harness.Window.DragDrop(target, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        harness.Window.DragDrop(target, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);

        await harness.WaitUntilAsync(() => harness.Session.Project?.Tracks.Any(track => track.Clips.Count > 0) == true);
        Assert.Single(harness.Session.RequireProject().Assets);
    }

    [AvaloniaFact]
    public async Task Dropping_a_file_on_the_viewer_imports_it_and_adds_it_to_the_end()
    {
        using var harness = EditorHarness.Open();
        var mediaPath = harness.Directory.Combine("clip.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        var data = await harness.FileDropAsync(mediaPath);
        var target = harness.CenterOf("Viewer");

        harness.Window.DragDrop(target, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        harness.Window.DragDrop(target, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);

        await harness.WaitUntilAsync(() => harness.Session.Project?.Tracks.Any(track => track.Clips.Count > 0) == true);
    }

    [AvaloniaFact]
    public async Task Dropping_library_media_on_the_timeline_places_it_where_it_lands()
    {
        using var harness = EditorHarness.Open();
        var data = await LibraryDragOfNewClipAsync(harness);
        var target = harness.CenterOf("TimelinePanel");

        harness.Window.DragDrop(target, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        harness.Window.DragDrop(target, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);

        await harness.WaitUntilAsync(() => harness.Session.RequireProject().Tracks.Any(track => track.Clips.Count > 0));
        Assert.True(harness.Session.RequireProject().Tracks.Single().Clips.Single().TimelineStartFrame > 0);
    }

    [AvaloniaFact]
    public async Task Dropping_library_media_on_the_viewer_adds_it_to_the_end()
    {
        using var harness = EditorHarness.Open();
        var data = await LibraryDragOfNewClipAsync(harness);
        var target = harness.CenterOf("Viewer");

        harness.Window.DragDrop(target, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        harness.Window.DragDrop(target, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);

        await harness.WaitUntilAsync(() => harness.Session.RequireProject().Tracks.Any(track => track.Clips.Count > 0));
        Assert.Equal(0, harness.Session.RequireProject().Tracks.Single().Clips.Single().TimelineStartFrame);
    }

    private static async Task<DataTransfer> LibraryDragOfNewClipAsync(EditorHarness harness)
    {
        var mediaPath = harness.Directory.Combine("clip.mp4");
        await TestMedia.GenerateClipAsync(mediaPath);
        await harness.ViewModel.ImportFilesAsync([mediaPath], trackId: null, dropPixels: null);
        harness.ViewModel.ToggleAdvancedModeCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var assetId = harness.Session.RequireProject().Assets.Single().Id;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(DragFormats.LibraryAsset, assetId.ToString()));
        return data;
    }
}
