using Vineeeeea.Application.Config;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Tests;

public class ControllerTests
{
    private const string MediaPath = @"C:\media\clip.mp4";
    private const long ThreeSecondsUs = 3_000_000;

    private static readonly AppConfig Config = AppConfig.FromEnvironment(_ => null);

    private sealed class FakeProbe(ProbedMedia result) : IMediaProbe
    {
        public Task<ProbedMedia> ProbeAsync(string filePath, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FakeRecentProjects : IRecentProjects
    {
        public List<RecentProject> Remembered { get; } = [];

        public IReadOnlyList<RecentProject> List() => Remembered;

        public void Remember(RecentProject project) => Remembered.Insert(0, project);
    }

    private sealed class FakeStore : IProjectStore
    {
        public Project? Saved { get; private set; }

        public Task SaveAsync(Project project, string filePath, CancellationToken cancellationToken)
        {
            Saved = project;
            return Task.CompletedTask;
        }

        public Task<Project> LoadAsync(string filePath, CancellationToken cancellationToken) =>
            Task.FromResult(Saved ?? throw new NotFoundException("vacío"));
    }

    private static EditorSession OpenSession()
    {
        var session = new EditorSession();
        new ProjectFileController(session, new FakeStore(), new FakeRecentProjects(), Config).CreateNew("Prueba");
        return session;
    }

    private static ProbedStream VideoStream(bool attachedPicture = false) => new(ProbedStreamType.Video, "h264", 1280, 720, attachedPicture);

    private static readonly ProbedStream AudioStream = new(ProbedStreamType.Audio, "aac", null, null, false);

    [Fact]
    public async Task Imports_video_with_its_audio_flag()
    {
        var session = OpenSession();
        var controller = new MediaImportController(session, new FakeProbe(new ProbedMedia(ThreeSecondsUs, [VideoStream(), AudioStream])));

        var asset = await controller.ImportAsync(MediaPath, CancellationToken.None);

        Assert.Equal(MediaKind.Video, asset.Kind);
        Assert.True(asset.HasAudio);
        Assert.Equal((1280, 720), (asset.Width, asset.Height));
        Assert.Single(session.RequireProject().Assets);
        Assert.True(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task Treats_cover_art_with_audio_as_audio()
    {
        var controller = new MediaImportController(OpenSession(), new FakeProbe(new ProbedMedia(ThreeSecondsUs, [VideoStream(attachedPicture: true), AudioStream])));

        var asset = await controller.ImportAsync(MediaPath, CancellationToken.None);

        Assert.Equal(MediaKind.Audio, asset.Kind);
    }

    [Fact]
    public async Task Rejects_media_without_duration()
    {
        var controller = new MediaImportController(OpenSession(), new FakeProbe(new ProbedMedia(null, [VideoStream()])));

        await Assert.ThrowsAsync<ValidationException>(() => controller.ImportAsync(MediaPath, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_relative_paths()
    {
        var controller = new MediaImportController(OpenSession(), new FakeProbe(new ProbedMedia(ThreeSecondsUs, [AudioStream])));

        await Assert.ThrowsAsync<ValidationException>(() => controller.ImportAsync(@"media\clip.mp4", CancellationToken.None));
    }

    [Fact]
    public async Task Import_requires_an_open_project()
    {
        var controller = new MediaImportController(new EditorSession(), new FakeProbe(new ProbedMedia(ThreeSecondsUs, [AudioStream])));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.ImportAsync(MediaPath, CancellationToken.None));
    }

    [Fact]
    public async Task Appended_clips_follow_each_other()
    {
        var session = OpenSession();
        var asset = await new MediaImportController(session, new FakeProbe(new ProbedMedia(ThreeSecondsUs, [VideoStream()])))
            .ImportAsync(MediaPath, CancellationToken.None);
        var timeline = new TimelineController(session);
        var track = timeline.AddTrack(TrackKind.Video);

        timeline.AppendClip(track.Id, asset.Id);
        var second = timeline.AppendClip(track.Id, asset.Id);

        Assert.Equal(90, second.TimelineStartFrame);
        Assert.Equal("Video 1", track.Name);
    }

    [Fact]
    public async Task Save_without_a_file_path_asks_for_save_as()
    {
        var session = OpenSession();
        var controller = new ProjectFileController(session, new FakeStore(), new FakeRecentProjects(), Config);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.SaveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Save_as_rejects_other_extensions_and_marks_saved_on_success()
    {
        var session = OpenSession();
        var store = new FakeStore();
        var controller = new ProjectFileController(session, store, new FakeRecentProjects(), Config);

        await Assert.ThrowsAsync<ValidationException>(() => controller.SaveAsAsync(@"C:\proyectos\demo.json", CancellationToken.None));
        await controller.SaveAsAsync(@"C:\proyectos\demo.vine", CancellationToken.None);

        Assert.False(session.HasUnsavedChanges);
        Assert.Same(session.Project, store.Saved);
    }

    [Fact]
    public void Config_rejects_an_invalid_timeout()
    {
        Assert.Throws<ValidationException>(() => AppConfig.FromEnvironment(_ => "-5"));
    }

    [Theory]
    [InlineData("png_pipe")]
    [InlineData("image2")]
    public async Task Imports_photos_as_still_images(string formatName)
    {
        var photo = new ProbedMedia(null, [new ProbedStream(ProbedStreamType.Video, "png", 1080, 1350, false)], formatName);
        var controller = new MediaImportController(OpenSession(), new FakeProbe(photo));

        var asset = await controller.ImportAsync(@"C:\fotos\playa.png", CancellationToken.None);

        Assert.Equal(MediaKind.Image, asset.Kind);
        Assert.Equal((1080, 1350), (asset.Width, asset.Height));
        Assert.False(asset.HasAudio);
    }

    [Fact]
    public async Task Saving_and_opening_remember_the_project_as_recent()
    {
        var session = OpenSession();
        var store = new FakeStore();
        var recents = new FakeRecentProjects();
        var controller = new ProjectFileController(session, store, recents, Config);

        await controller.SaveAsAsync(@"C:\proyectos\demo.vine", CancellationToken.None);
        await controller.OpenAsync(@"C:\proyectos\demo.vine", CancellationToken.None);

        Assert.Equal(2, recents.Remembered.Count);
        Assert.All(recents.Remembered, recent => Assert.Equal((@"C:\proyectos\demo.vine", "Prueba"), (recent.FilePath, recent.Name)));
    }

    [Theory]
    [InlineData(CanvasShape.Horizontal, 1920, 1080)]
    [InlineData(CanvasShape.Vertical, 1080, 1920)]
    [InlineData(CanvasShape.Square, 1080, 1080)]
    public void New_projects_take_the_chosen_shape_rate_and_audio(CanvasShape shape, int width, int height)
    {
        var session = new EditorSession();

        new ProjectFileController(session, new FakeStore(), new FakeRecentProjects(), Config)
            .CreateNew("Nuevo", shape, new FrameRate(25, 1), 44_100);

        var settings = session.RequireProject().Settings;
        Assert.Equal((width, height, 25, 44_100), (settings.Width, settings.Height, settings.FrameRate.Numerator, settings.AudioSampleRate));
    }

    [Fact]
    public async Task Matching_the_canvas_to_a_vertical_video_makes_the_project_vertical()
    {
        var session = OpenSession();
        var asset = await new MediaImportController(session, new FakeProbe(new ProbedMedia(ThreeSecondsUs, [new ProbedStream(ProbedStreamType.Video, "h264", 720, 1280, false)])))
            .ImportAsync(MediaPath, CancellationToken.None);

        new ProjectSettingsController(session).MatchCanvasTo(asset.Id);

        Assert.Equal((1080, 1920), (session.RequireProject().Settings.Width, session.RequireProject().Settings.Height));
    }
}
