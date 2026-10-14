using Vineeeeea.Application.Config;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Application.Tests;

public class CaptionsControllerTests
{
    private const long TenSecondsUs = 10_000_000;

    private static readonly AppConfig Config = AppConfig.FromEnvironment(_ => null);

    private sealed class FakeTranscriber(params TranscriptSegment[] segments) : ISpeechTranscriber
    {
        public (long StartUs, long DurationUs)? Requested { get; private set; }

        public Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(string mediaPath, long sourceStartUs, long sourceDurationUs, CancellationToken cancellationToken)
        {
            Requested = (sourceStartUs, sourceDurationUs);
            return Task.FromResult<IReadOnlyList<TranscriptSegment>>(segments);
        }
    }

    private static (EditorSession Session, Clip Clip) SessionWithClip(bool hasAudio, double speed = 1)
    {
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, @"C:\media\charla.mp4", TenSecondsUs, 1920, 1080, hasAudio);
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var clip = new Clip(Guid.NewGuid(), video.Id, 30, 2_000_000, 6_000_000, speed, []);
        var session = new EditorSession();
        session.Open(new Project(Guid.NewGuid(), "Charla", Config.DefaultProjectSettings).AddAsset(video).AddTrack(track).InsertClip(track.Id, clip), filePath: null);
        return (session, clip);
    }

    [Fact]
    public async Task Captions_follow_the_clip_position_and_speed_in_one_undo_step()
    {
        var (session, clip) = SessionWithClip(hasAudio: true, speed: 2);
        var transcriber = new FakeTranscriber(new(0, 2_000_000, " Hola a todos. "), new(2_000_000, 4_000_000, "Bienvenidos"), new(4_000_000, 4_500_000, "   "));

        var count = await new CaptionsController(session, transcriber).GenerateAsync(clip.Id, CancellationToken.None);

        var project = session.RequireProject();
        var captionTrack = project.Tracks.Single(track => track.Kind == TrackKind.Text);
        Assert.Equal(2, count);
        Assert.Equal((2_000_000L, 6_000_000L), transcriber.Requested);
        Assert.Equal([30L, 60L], captionTrack.Clips.Select(caption => caption.TimelineStartFrame));
        Assert.Equal("Hola a todos.", project.GetAsset(captionTrack.Clips[0].AssetId).Text!.Content);
        Assert.Equal(0.7, captionTrack.Clips[0].FindEffect(EffectCatalog.Transform)!.Parameters[EffectCatalog.PositionY]);

        session.Undo();
        Assert.DoesNotContain(session.RequireProject().Tracks, track => track.Kind == TrackKind.Text);
    }

    [Fact]
    public async Task Clips_without_sound_cannot_be_captioned()
    {
        var (session, clip) = SessionWithClip(hasAudio: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new CaptionsController(session, new FakeTranscriber()).GenerateAsync(clip.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Silence_is_reported_instead_of_creating_an_empty_track()
    {
        var (session, clip) = SessionWithClip(hasAudio: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new CaptionsController(session, new FakeTranscriber(new TranscriptSegment(0, 1_000_000, " "))).GenerateAsync(clip.Id, CancellationToken.None));
        Assert.Single(session.RequireProject().Tracks);
    }

    [Theory]
    [InlineData("español")]
    [InlineData("es; rm")]
    public void Config_rejects_invalid_caption_languages(string language)
    {
        Assert.Throws<ValidationException>(() =>
            AppConfig.FromEnvironment(variable => variable == "VINEEEEEA_CAPTION_LANGUAGE" ? language : null));
    }
}
