using Vineeeeea.Application.Config;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Tests;

public class PlaybackControllerTests
{
    private const long OneSecondUs = 1_000_000;
    private const long FifthOfASecondUs = 200_000;

    private static readonly AppConfig Config = AppConfig.FromEnvironment(_ => null);

    private sealed class FakeRenderEngine : IRenderEngine
    {
        public List<RenderPlan> Plans { get; } = [];

        public async Task StreamVideoAsync(RenderPlan plan, Func<byte[], CancellationToken, ValueTask> onFrame, CancellationToken cancellationToken)
        {
            Plans.Add(plan);
            for (var index = 0; index < plan.DurationFrames; index++)
            {
                await onFrame(new byte[plan.OutputSize.FrameBytes], cancellationToken);
            }
        }

        public Task ExportAsync(RenderPlan plan, string outputPath, ExportEncoding encoding, IProgress<double> progress, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeAudioPlayer : IAudioPlayer
    {
        public int Plays { get; private set; }

        public async Task PlayAsync(RenderPlan plan, CancellationToken cancellationToken)
        {
            Plays++;
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private static (EditorSession Session, MediaAsset Video, Track Track) SessionWithVideoTrack(bool hasAudio = false)
    {
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, TestPaths.Abs(@"C:\media\clip.mp4"), 10 * OneSecondUs, 1920, 1080, hasAudio);
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var session = new EditorSession();
        session.Open(new Project(Guid.NewGuid(), "Prueba", Config.DefaultProjectSettings).AddAsset(video).AddTrack(track), filePath: null);
        return (session, video, track);
    }

    [Fact]
    public void Preview_keeps_the_canvas_aspect_within_the_configured_size()
    {
        var controller = new PlaybackController(new EditorSession(), new FakeRenderEngine(), new FakeAudioPlayer(), Config);

        Assert.Equal(new FrameSize(640, 360), controller.PreviewSizeFor(new ProjectSettings(1920, 1080, new FrameRate(30, 1), 48_000)));
        Assert.Equal(new FrameSize(360, 640), controller.PreviewSizeFor(new ProjectSettings(1080, 1920, new FrameRate(30, 1), 48_000)));
    }

    [Fact]
    public async Task A_still_past_the_end_is_black_and_renders_nothing()
    {
        var (session, _, _) = SessionWithVideoTrack();
        var engine = new FakeRenderEngine();

        var still = await new PlaybackController(session, engine, new FakeAudioPlayer(), Config).RenderStillAsync(0, CancellationToken.None);

        Assert.Null(still.Bgra);
        Assert.Empty(engine.Plans);
    }

    [Fact]
    public async Task A_still_renders_a_one_frame_plan_at_the_playhead()
    {
        var (session, video, track) = SessionWithVideoTrack();
        session.Apply(project => project.InsertClip(track.Id, new Clip(Guid.NewGuid(), video.Id, 0, OneSecondUs, OneSecondUs, 1, [])));
        var engine = new FakeRenderEngine();

        var still = await new PlaybackController(session, engine, new FakeAudioPlayer(), Config).RenderStillAsync(15, CancellationToken.None);

        var plan = Assert.Single(engine.Plans);
        Assert.Equal(1, plan.DurationFrames);
        Assert.Equal(1_500_000, Assert.Single(plan.Layers).SourceStartUs);
        Assert.NotNull(still.Bgra);
    }

    [Fact]
    public async Task Playback_delivers_every_frame_in_order_and_stops_the_audio_at_the_end()
    {
        var (session, video, track) = SessionWithVideoTrack(hasAudio: true);
        session.Apply(project => project.InsertClip(track.Id, new Clip(Guid.NewGuid(), video.Id, 3, 0, FifthOfASecondUs, 1, [])));
        var audio = new FakeAudioPlayer();
        var delivered = new List<VideoFrame>();

        await new PlaybackController(session, new FakeRenderEngine(), audio, Config).PlayAsync(0, delivered.Add, CancellationToken.None);

        Assert.Equal(Enumerable.Range(0, 9).Select(frame => (long)frame), delivered.Select(frame => frame.TimelineFrame));
        Assert.Equal(1, audio.Plays);
    }

    [Fact]
    public async Task Playback_from_the_last_frame_restarts_from_the_beginning()
    {
        var (session, video, track) = SessionWithVideoTrack();
        session.Apply(project => project.InsertClip(track.Id, new Clip(Guid.NewGuid(), video.Id, 0, 0, FifthOfASecondUs, 1, [])));
        var delivered = new List<VideoFrame>();

        await new PlaybackController(session, new FakeRenderEngine(), new FakeAudioPlayer(), Config).PlayAsync(5, delivered.Add, CancellationToken.None);

        Assert.Equal(0, delivered[0].TimelineFrame);
    }
}
