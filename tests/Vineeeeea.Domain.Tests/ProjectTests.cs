using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Domain.Tests;

public class ProjectTests
{
    private const long TenSecondsUs = 10_000_000;
    private const long TwoSecondsUs = 2_000_000;
    private const long FramesInTwoSecondsAt30 = 60;

    private static readonly MediaAsset Video = new(Guid.NewGuid(), MediaKind.Video, @"C:\media\a.mp4", TenSecondsUs, 1920, 1080, hasAudio: true);
    private static readonly MediaAsset Audio = new(Guid.NewGuid(), MediaKind.Audio, @"C:\media\a.wav", TenSecondsUs, null, null, hasAudio: true);
    private static readonly Track VideoTrack = new(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
    private static readonly Track SecondVideoTrack = new(Guid.NewGuid(), TrackKind.Video, "Video 2", isMuted: false, isHidden: false);

    private static Project NewProject() =>
        new Project(Guid.NewGuid(), "Prueba", new ProjectSettings(1920, 1080, new FrameRate(30, 1), 48_000))
            .AddAsset(Video)
            .AddAsset(Audio)
            .AddTrack(VideoTrack)
            .AddTrack(SecondVideoTrack);

    private static Clip ClipOf(MediaAsset asset, long startFrame, long sourceInUs = 0, long durationUs = TwoSecondsUs) =>
        new(Guid.NewGuid(), asset.Id, startFrame, sourceInUs, durationUs, speed: 1, effects: []);

    [Fact]
    public void Inserted_clips_stay_sorted_by_start()
    {
        var project = NewProject()
            .InsertClip(VideoTrack.Id, ClipOf(Video, FramesInTwoSecondsAt30))
            .InsertClip(VideoTrack.Id, ClipOf(Video, 0));

        Assert.Equal([0, FramesInTwoSecondsAt30], project.GetTrack(VideoTrack.Id).Clips.Select(clip => clip.TimelineStartFrame));
    }

    [Fact]
    public void Rejects_overlapping_clips_on_the_same_track()
    {
        var project = NewProject().InsertClip(VideoTrack.Id, ClipOf(Video, 0));

        Assert.Throws<BusinessRuleException>(() => project.InsertClip(VideoTrack.Id, ClipOf(Video, FramesInTwoSecondsAt30 - 1)));
    }

    [Fact]
    public void Rejects_media_of_the_wrong_kind_for_the_track()
    {
        Assert.Throws<BusinessRuleException>(() => NewProject().InsertClip(VideoTrack.Id, ClipOf(Audio, 0)));
    }

    [Fact]
    public void Rejects_a_source_range_beyond_the_media_duration()
    {
        var clip = ClipOf(Video, 0, sourceInUs: TenSecondsUs - 1, durationUs: TwoSecondsUs);

        Assert.Throws<BusinessRuleException>(() => NewProject().InsertClip(VideoTrack.Id, clip));
    }

    [Fact]
    public void Rejects_unknown_media()
    {
        var clip = new Clip(Guid.NewGuid(), Guid.NewGuid(), 0, 0, TwoSecondsUs, 1, []);

        Assert.Throws<NotFoundException>(() => NewProject().InsertClip(VideoTrack.Id, clip));
    }

    [Fact]
    public void Rejects_duplicate_clip_ids()
    {
        var clip = ClipOf(Video, 0);
        var project = NewProject().InsertClip(VideoTrack.Id, clip);

        Assert.Throws<ValidationException>(() => project.InsertClip(SecondVideoTrack.Id, clip));
    }

    [Fact]
    public void Moves_a_clip_to_another_track_and_position()
    {
        var clip = ClipOf(Video, 0);
        var project = NewProject().InsertClip(VideoTrack.Id, clip);

        var moved = project.MoveClip(clip.Id, SecondVideoTrack.Id, 90);

        Assert.Empty(moved.GetTrack(VideoTrack.Id).Clips);
        Assert.Equal(90, Assert.Single(moved.GetTrack(SecondVideoTrack.Id).Clips).TimelineStartFrame);
    }

    [Fact]
    public void A_rejected_move_leaves_the_original_project_unchanged()
    {
        var first = ClipOf(Video, 0);
        var second = ClipOf(Video, FramesInTwoSecondsAt30);
        var project = NewProject().InsertClip(VideoTrack.Id, first).InsertClip(VideoTrack.Id, second);

        Assert.Throws<BusinessRuleException>(() => project.MoveClip(second.Id, VideoTrack.Id, 10));
        Assert.Equal(FramesInTwoSecondsAt30, project.GetTrack(VideoTrack.Id).Clips[1].TimelineStartFrame);
    }

    [Fact]
    public void Shifting_a_clip_left_stops_at_the_start_of_the_timeline()
    {
        var clip = ClipOf(Video, 30);
        var project = NewProject().InsertClip(VideoTrack.Id, clip);

        var shifted = project.ShiftClip(clip.Id, -500);

        Assert.Equal(0, Assert.Single(shifted.GetTrack(VideoTrack.Id).Clips).TimelineStartFrame);
    }

    [Fact]
    public void Clip_duration_on_the_timeline_follows_speed()
    {
        var clip = new Clip(Guid.NewGuid(), Video.Id, 0, 0, TwoSecondsUs, speed: 2, []);

        Assert.Equal(FramesInTwoSecondsAt30 / 2, clip.TimelineDurationFrames(new FrameRate(30, 1)));
    }
}
