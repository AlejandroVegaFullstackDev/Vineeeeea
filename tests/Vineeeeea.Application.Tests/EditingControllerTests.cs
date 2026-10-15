using Vineeeeea.Application.Config;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Application.Tests;

public class EditingControllerTests
{
    private const long FourSecondsUs = 4_000_000;

    private static readonly AppConfig Config = AppConfig.FromEnvironment(_ => null);

    private sealed class RecordingRenderEngine : IRenderEngine
    {
        public RenderPlan? Exported { get; private set; }

        public Task StreamVideoAsync(RenderPlan plan, Func<byte[], CancellationToken, ValueTask> onFrame, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ExportEncoding? Encoding { get; private set; }

        public Task ExportAsync(RenderPlan plan, string outputPath, ExportEncoding encoding, IProgress<double> progress, CancellationToken cancellationToken)
        {
            Exported = plan;
            Encoding = encoding;
            return Task.CompletedTask;
        }
    }

    private static (EditorSession Session, TimelineController Timeline, Guid ClipId) SessionWithClip()
    {
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, TestPaths.Abs(@"C:\media\clip.mp4"), FourSecondsUs, 1920, 1080, hasAudio: true);
        var session = new EditorSession();
        session.Open(new Project(Guid.NewGuid(), "Edición", Config.DefaultProjectSettings).AddAsset(video), filePath: null);
        var timeline = new TimelineController(session);
        var track = timeline.AddTrack(TrackKind.Video);
        var clip = timeline.AppendClip(track.Id, video.Id);
        return (session, timeline, clip.Id);
    }

    [Fact]
    public void Undo_and_redo_walk_the_edit_history()
    {
        var (session, timeline, clipId) = SessionWithClip();
        var history = new HistoryController(session);

        timeline.SplitClip(clipId, 30);
        history.Undo();
        Assert.Single(session.RequireProject().Tracks[0].Clips);

        history.Redo();
        Assert.Equal(2, session.RequireProject().Tracks[0].Clips.Count);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void A_new_edit_clears_redo()
    {
        var (session, timeline, clipId) = SessionWithClip();
        var history = new HistoryController(session);

        timeline.ShiftClip(clipId, 10);
        history.Undo();
        timeline.ShiftClip(clipId, 20);

        Assert.False(history.CanRedo);
        Assert.Throws<BusinessRuleException>(history.Redo);
    }

    [Fact]
    public void Dragging_a_slider_is_undone_in_one_step()
    {
        var (session, timeline, clipId) = SessionWithClip();
        var history = new HistoryController(session);

        foreach (var value in new[] { 0.1, 0.2, 0.3 })
        {
            timeline.SetEffectParameter(clipId, EffectCatalog.Brightness, EffectCatalog.Amount, value, playheadFrame: 0);
        }

        history.Undo();

        Assert.Null(session.RequireProject().GetClip(clipId).FindEffect(EffectCatalog.Brightness));
    }

    [Fact]
    public void Editing_an_animated_parameter_moves_the_keyframe_under_the_playhead()
    {
        var (session, timeline, clipId) = SessionWithClip();

        timeline.AddKeyframe(clipId, EffectCatalog.Volume, EffectCatalog.Gain, playheadFrame: 0);
        timeline.AddKeyframe(clipId, EffectCatalog.Volume, EffectCatalog.Gain, playheadFrame: 60);
        timeline.SetEffectParameter(clipId, EffectCatalog.Volume, EffectCatalog.Gain, 0, playheadFrame: 60);

        var volume = session.RequireProject().GetClip(clipId).FindEffect(EffectCatalog.Volume)!;
        Assert.Equal(0.5, volume.ValueAt(EffectCatalog.Gain, 1_000_000), precision: 6);
        Assert.Equal(1, volume.Parameters[EffectCatalog.Gain]);
    }

    [Fact]
    public void Keyframes_need_the_playhead_on_the_clip()
    {
        var (_, timeline, clipId) = SessionWithClip();

        Assert.Throws<BusinessRuleException>(() => timeline.AddKeyframe(clipId, EffectCatalog.Volume, EffectCatalog.Gain, playheadFrame: 500));
    }

    [Fact]
    public void Unsaved_changes_follow_the_saved_snapshot_through_undo()
    {
        var (session, timeline, clipId) = SessionWithClip();
        session.MarkSaved(TestPaths.Abs(@"C:\proyectos\demo.vine"));

        timeline.ShiftClip(clipId, 5);
        Assert.True(session.HasUnsavedChanges);

        session.Undo();
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task Export_renders_the_whole_timeline_at_the_destination_size()
    {
        var (session, _, _) = SessionWithClip();
        var engine = new RecordingRenderEngine();
        var whatsApp = ExportDestinations.Get(ExportDestinations.WhatsApp);

        await new ExportController(session, engine).ExportAsync(whatsApp, TestPaths.Abs(@"C:\videos\final.mp4"), new Progress<double>(), CancellationToken.None);

        Assert.Equal(new FrameSize(1280, 720), engine.Exported!.OutputSize);
        Assert.Equal(120, engine.Exported.DurationFrames);
        Assert.Equal(whatsApp.Encoding, engine.Encoding);
    }

    [Fact]
    public async Task Export_rejects_empty_projects_and_other_formats()
    {
        var session = new EditorSession();
        session.Open(new Project(Guid.NewGuid(), "Vacío", Config.DefaultProjectSettings), filePath: null);
        var controller = new ExportController(session, new RecordingRenderEngine());
        var youTube = ExportDestinations.Get(ExportDestinations.YouTube);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.ExportAsync(youTube, TestPaths.Abs(@"C:\videos\final.mp4"), new Progress<double>(), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => controller.ExportAsync(youTube, TestPaths.Abs(@"C:\videos\final.avi"), new Progress<double>(), CancellationToken.None));
    }

    [Fact]
    public void Dropping_media_without_a_matching_track_creates_one_in_a_single_undo_step()
    {
        var music = new MediaAsset(Guid.NewGuid(), MediaKind.Audio, TestPaths.Abs(@"C:\media\musica.mp3"), FourSecondsUs, null, null, hasAudio: true);
        var session = new EditorSession();
        session.Open(new Project(Guid.NewGuid(), "Soltar", Config.DefaultProjectSettings).AddAsset(music), filePath: null);
        var timeline = new TimelineController(session);
        var videoTrack = timeline.AddTrack(TrackKind.Video);

        var clip = timeline.PlaceAsset(music.Id, videoTrack.Id, startFrame: 45);

        var project = session.RequireProject();
        Assert.Equal(TrackKind.Audio, project.GetTrackOfClip(clip.Id).Kind);
        Assert.Equal(45, clip.TimelineStartFrame);
        session.Undo();
        Assert.Single(session.RequireProject().Tracks);
    }

    [Fact]
    public void Dropping_media_on_a_compatible_track_places_it_at_the_frame()
    {
        var (session, timeline, clipId) = SessionWithClip();
        var project = session.RequireProject();
        var track = project.GetTrackOfClip(clipId);

        var placed = timeline.PlaceAsset(project.GetClip(clipId).AssetId, track.Id, startFrame: 200);

        Assert.Equal(track.Id, session.RequireProject().GetTrackOfClip(placed.Id).Id);
        Assert.Equal(200, placed.TimelineStartFrame);
    }

    [Fact]
    public void Dropping_on_top_of_another_clip_is_rejected()
    {
        var (session, timeline, clipId) = SessionWithClip();
        var project = session.RequireProject();

        Assert.Throws<BusinessRuleException>(() =>
            timeline.PlaceAsset(project.GetClip(clipId).AssetId, project.GetTrackOfClip(clipId).Id, startFrame: 10));
    }

    [Fact]
    public void Appending_media_goes_to_the_end_of_the_last_matching_track()
    {
        var (session, timeline, clipId) = SessionWithClip();
        var assetId = session.RequireProject().GetClip(clipId).AssetId;

        var appended = timeline.AppendAssetToTimeline(assetId);

        Assert.Equal(120, appended.TimelineStartFrame);
        Assert.Single(session.RequireProject().Tracks);
    }

    [Fact]
    public void Removing_media_from_the_library_can_be_undone()
    {
        var (session, _, clipId) = SessionWithClip();
        var assetId = session.RequireProject().GetClip(clipId).AssetId;

        new MediaLibraryController(session).RemoveAsset(assetId);
        Assert.Empty(session.RequireProject().Assets);

        session.Undo();
        Assert.NotNull(session.RequireProject().TryGetClip(clipId));
    }

    [Fact]
    public void Adding_text_creates_a_text_track_and_moves_to_a_new_one_when_it_would_overlap()
    {
        var (session, timeline, _) = SessionWithClip();

        var first = timeline.AddText(atFrame: 10);
        var second = timeline.AddText(atFrame: 20);
        timeline.UpdateText(second.Id, TextStyle.Default("Segundo"));

        var project = session.RequireProject();
        Assert.Equal(2, project.Tracks.Count(track => track.Kind == TrackKind.Text));
        Assert.NotEqual(project.GetTrackOfClip(first.Id).Id, project.GetTrackOfClip(second.Id).Id);
        Assert.Equal("Segundo", project.GetAsset(project.GetClip(second.Id).AssetId).Text!.Content);
        session.Undo();
        session.Undo();
        Assert.Null(session.RequireProject().TryGetClip(second.Id));
    }
}
