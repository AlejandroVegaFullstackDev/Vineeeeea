using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Domain.Tests;

public class EditingTests
{
    private const long TenSecondsUs = 10_000_000;
    private const long TwoSecondsUs = 2_000_000;
    private const long OneSecondUs = 1_000_000;

    private static readonly FrameRate ThirtyFps = new(30, 1);
    private static readonly MediaAsset Video = new(Guid.NewGuid(), MediaKind.Video, @"C:\media\a.mp4", TenSecondsUs, 1920, 1080, hasAudio: true);
    private static readonly MediaAsset SilentVideo = new(Guid.NewGuid(), MediaKind.Video, @"C:\media\mudo.mp4", TenSecondsUs, 1920, 1080, hasAudio: false);
    private static readonly MediaAsset Audio = new(Guid.NewGuid(), MediaKind.Audio, @"C:\media\a.wav", TenSecondsUs, null, null, hasAudio: true);
    private static readonly Track VideoTrack = new(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
    private static readonly Track AudioTrack = new(Guid.NewGuid(), TrackKind.Audio, "Audio 1", isMuted: false, isHidden: false);

    private static (Project Project, Clip Clip) ProjectWithClip(MediaAsset asset, Track track, long startFrame = 0)
    {
        var clip = new Clip(Guid.NewGuid(), asset.Id, startFrame, OneSecondUs, TwoSecondsUs, speed: 1, []);
        var project = new Project(Guid.NewGuid(), "Edición", new ProjectSettings(1920, 1080, ThirtyFps, 48_000))
            .AddAsset(Video)
            .AddAsset(SilentVideo)
            .AddAsset(Audio)
            .AddTrack(VideoTrack)
            .AddTrack(AudioTrack)
            .InsertClip(track.Id, clip);
        return (project, clip);
    }

    [Fact]
    public void Splitting_produces_two_adjacent_clips_covering_the_same_source()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var secondId = Guid.NewGuid();

        var clips = project.SplitClip(clip.Id, 15, secondId).GetTrack(VideoTrack.Id).Clips;

        Assert.Equal(2, clips.Count);
        Assert.Equal((0L, OneSecondUs, 500_000L), (clips[0].TimelineStartFrame, clips[0].SourceInUs, clips[0].SourceDurationUs));
        Assert.Equal((15L, 1_500_000L, 1_500_000L), (clips[1].TimelineStartFrame, clips[1].SourceInUs, clips[1].SourceDurationUs));
        Assert.Equal(secondId, clips[1].Id);
        Assert.Equal(clips[0].TimelineEndFrame(ThirtyFps), clips[1].TimelineStartFrame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    public void Splitting_outside_the_clip_is_rejected(long atFrame)
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);

        Assert.Throws<BusinessRuleException>(() => project.SplitClip(clip.Id, atFrame, Guid.NewGuid()));
    }

    [Fact]
    public void Trimming_the_start_keeps_the_content_anchored()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack, startFrame: 30);

        var trimmed = project.TrimClipStart(clip.Id, 15).GetClip(clip.Id);

        Assert.Equal((45L, 1_500_000L, 1_500_000L), (trimmed.TimelineStartFrame, trimmed.SourceInUs, trimmed.SourceDurationUs));
    }

    [Fact]
    public void Trimming_cannot_reveal_media_that_does_not_exist()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack, startFrame: 60);

        Assert.Throws<ValidationException>(() => project.TrimClipStart(clip.Id, -31));
        Assert.Throws<BusinessRuleException>(() => project.TrimClipEnd(clip.Id, 300));
        Assert.Throws<ValidationException>(() => project.TrimClipEnd(clip.Id, -60));
    }

    [Fact]
    public void Removing_a_clip_empties_its_track()
    {
        var (project, clip) = ProjectWithClip(Audio, AudioTrack);

        Assert.Empty(project.RemoveClip(clip.Id).GetTrack(AudioTrack.Id).Clips);
    }

    [Fact]
    public void Effects_are_upserted_by_type_and_validated_against_the_media()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var brighter = EffectInstance.CreateDefault(EffectCatalog.Brightness).WithParameter(EffectCatalog.Amount, 0.3);

        var updated = project
            .SetClipEffect(clip.Id, EffectInstance.CreateDefault(EffectCatalog.Brightness))
            .SetClipEffect(clip.Id, brighter);

        Assert.Equal(0.3, Assert.Single(updated.GetClip(clip.Id).Effects).Parameters[EffectCatalog.Amount]);
    }

    [Fact]
    public void Audio_effects_need_audio_and_visual_effects_need_video()
    {
        var (silentProject, silentClip) = ProjectWithClip(SilentVideo, VideoTrack);
        var (audioProject, audioClip) = ProjectWithClip(Audio, AudioTrack);

        Assert.Throws<BusinessRuleException>(() => silentProject.SetClipEffect(silentClip.Id, EffectInstance.CreateDefault(EffectCatalog.Volume)));
        Assert.Throws<BusinessRuleException>(() => audioProject.SetClipEffect(audioClip.Id, EffectInstance.CreateDefault(EffectCatalog.Opacity)));
    }

    [Fact]
    public void Effect_parameters_outside_their_range_are_rejected()
    {
        Assert.Throws<ValidationException>(() =>
            EffectInstance.CreateDefault(EffectCatalog.Opacity).WithParameter(EffectCatalog.Amount, 1.5));
        Assert.Throws<ValidationException>(() =>
            EffectInstance.CreateDefault(EffectCatalog.Opacity).WithKeyframe(EffectCatalog.Amount, 0, 0.5));
        Assert.Throws<ValidationException>(() => EffectInstance.CreateDefault("glitch"));
    }

    [Fact]
    public void Keyframes_interpolate_linearly_and_hold_their_ends()
    {
        var volume = EffectInstance.CreateDefault(EffectCatalog.Volume)
            .WithKeyframe(EffectCatalog.Gain, 2_000_000, 0)
            .WithKeyframe(EffectCatalog.Gain, 1_000_000, 1);

        Assert.Equal(1, volume.ValueAt(EffectCatalog.Gain, 0));
        Assert.Equal(0.5, volume.ValueAt(EffectCatalog.Gain, 1_500_000), precision: 6);
        Assert.Equal(0, volume.ValueAt(EffectCatalog.Gain, 9_000_000));
        Assert.Equal(1, volume.WithoutKeyframes(EffectCatalog.Gain).ValueAt(EffectCatalog.Gain, 1_500_000));
    }

    [Fact]
    public void Splitting_keeps_effects_and_keyframes_on_both_halves()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var fade = EffectInstance.CreateDefault(EffectCatalog.Brightness)
            .WithKeyframe(EffectCatalog.Amount, OneSecondUs, -1)
            .WithKeyframe(EffectCatalog.Amount, 3 * OneSecondUs, 0);

        var clips = project.SetClipEffect(clip.Id, fade).SplitClip(clip.Id, 30, Guid.NewGuid()).GetTrack(VideoTrack.Id).Clips;

        Assert.All(clips, part => Assert.Equal(-0.5, part.FindEffect(EffectCatalog.Brightness)!.ValueAt(EffectCatalog.Amount, 2 * OneSecondUs), precision: 6));
    }

    [Fact]
    public void Source_time_at_a_timeline_frame_follows_speed()
    {
        var fast = new Clip(Guid.NewGuid(), Video.Id, 30, OneSecondUs, TwoSecondsUs, speed: 2, []);

        Assert.Equal(2_000_000, fast.SourceUsAt(45, ThirtyFps));
    }

    [Fact]
    public void Still_images_go_on_video_tracks_take_visual_effects_and_can_be_stretched()
    {
        var photo = MediaAsset.StillImage(Guid.NewGuid(), @"C:\media\foto.png", 1080, 1350);
        var clip = new Clip(Guid.NewGuid(), photo.Id, 0, 0, photo.DefaultClipDurationUs, 1, [EffectInstance.CreateDefault(EffectCatalog.Transform)]);
        var project = new Project(Guid.NewGuid(), "Fotos", new ProjectSettings(1920, 1080, ThirtyFps, 48_000))
            .AddAsset(photo)
            .AddTrack(VideoTrack)
            .AddTrack(AudioTrack)
            .InsertClip(VideoTrack.Id, clip);

        var stretched = project.TrimClipEnd(clip.Id, 30 * 60).GetClip(clip.Id);

        Assert.Equal(DomainLimits.DefaultImageClipUs + 60_000_000, stretched.SourceDurationUs);
        Assert.Throws<BusinessRuleException>(() => project.MoveClip(clip.Id, AudioTrack.Id, 0));
        Assert.Throws<BusinessRuleException>(() => project.SetClipEffect(clip.Id, EffectInstance.CreateDefault(EffectCatalog.Volume)));
    }

    [Fact]
    public void Duplicating_places_the_copy_right_after_the_original()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var copyId = Guid.NewGuid();

        var copy = project.DuplicateClip(clip.Id, copyId).GetClip(copyId);

        Assert.Equal((60L, clip.SourceInUs, clip.SourceDurationUs), (copy.TimelineStartFrame, copy.SourceInUs, copy.SourceDurationUs));
    }

    [Fact]
    public void Removing_media_removes_its_clips_from_every_track()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);

        var updated = project.RemoveAsset(Video.Id);

        Assert.DoesNotContain(updated.Assets, asset => asset.Id == Video.Id);
        Assert.Null(updated.TryGetClip(clip.Id));
    }

    [Fact]
    public void Tracks_can_be_muted_hidden_and_removed()
    {
        var (project, _) = ProjectWithClip(Video, VideoTrack);

        var toggled = project.SetTrackMuted(VideoTrack.Id, true).SetTrackHidden(VideoTrack.Id, true);

        Assert.True(toggled.GetTrack(VideoTrack.Id).IsMuted);
        Assert.True(toggled.GetTrack(VideoTrack.Id).IsHidden);
        Assert.Single(toggled.GetTrack(VideoTrack.Id).Clips);
        Assert.DoesNotContain(project.RemoveTrack(VideoTrack.Id).Tracks, track => track.Id == VideoTrack.Id);
    }

    [Fact]
    public void All_effects_of_a_clip_can_be_removed_at_once()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var decorated = project
            .SetClipEffect(clip.Id, EffectInstance.CreateDefault(EffectCatalog.Brightness))
            .SetClipEffect(clip.Id, EffectInstance.CreateDefault(EffectCatalog.Volume));

        Assert.Empty(decorated.RemoveAllClipEffects(clip.Id).GetClip(clip.Id).Effects);
    }

    [Fact]
    public void Changing_speed_keeps_the_source_and_changes_the_length_on_the_timeline()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);

        var faster = project.SetClipSpeed(clip.Id, 2).GetClip(clip.Id);

        Assert.Equal((clip.SourceInUs, clip.SourceDurationUs), (faster.SourceInUs, faster.SourceDurationUs));
        Assert.Equal(30, faster.TimelineDurationFrames(ThirtyFps));
    }

    [Fact]
    public void Slowing_down_into_the_next_clip_is_rejected()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var crowded = project.InsertClip(VideoTrack.Id, new Clip(Guid.NewGuid(), Video.Id, 60, 0, OneSecondUs, 1, []));

        Assert.Throws<BusinessRuleException>(() => crowded.SetClipSpeed(clip.Id, 0.5));
    }

    [Fact]
    public void Changing_the_frame_rate_keeps_clips_in_place_in_time()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack, startFrame: 60);

        var atSixty = project.ChangeSettings(new ProjectSettings(1080, 1920, new FrameRate(60, 1), 48_000));

        Assert.Equal(120, atSixty.GetClip(clip.Id).TimelineStartFrame);
        Assert.Equal((1080, 1920), (atSixty.Settings.Width, atSixty.Settings.Height));
    }

    [Fact]
    public void Changing_the_frame_rate_never_makes_neighbours_overlap()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var neighbour = new Clip(Guid.NewGuid(), Video.Id, 60, 0, OneSecondUs, 1, []);
        var both = project.InsertClip(VideoTrack.Id, neighbour);

        var ntsc = both.ChangeSettings(new ProjectSettings(1920, 1080, new FrameRate(30000, 1001), 48_000));

        Assert.True(ntsc.GetClip(neighbour.Id).TimelineStartFrame >= ntsc.GetClip(clip.Id).TimelineEndFrame(ntsc.Settings.FrameRate));
    }

    [Fact]
    public void A_filter_replaces_the_previous_color_adjustments_but_keeps_other_effects()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var styled = project
            .SetClipEffect(clip.Id, EffectInstance.CreateDefault(EffectCatalog.Transform).WithParameter(EffectCatalog.Scale, 0.5))
            .ApplyFilter(clip.Id, FilterPresets.Get("Vintage"))
            .ApplyFilter(clip.Id, FilterPresets.Get("Blanco y negro"));

        var effects = styled.GetClip(clip.Id).Effects.Select(effect => effect.Type).Order().ToList();

        Assert.Equal([EffectCatalog.Contrast, EffectCatalog.Grayscale, EffectCatalog.Transform], effects);
        Assert.Equal([EffectCatalog.Transform], styled.RemoveColorFilters(clip.Id).GetClip(clip.Id).Effects.Select(effect => effect.Type));
    }

    [Fact]
    public void Copies_of_a_text_clip_get_their_own_text_and_removed_texts_are_cleaned_up()
    {
        var title = MediaAsset.CreateText(Guid.NewGuid(), TextStyle.Default("Hola"));
        var textTrack = new Track(Guid.NewGuid(), TrackKind.Text, "Texto 1", isMuted: false, isHidden: false);
        var clip = new Clip(Guid.NewGuid(), title.Id, 0, 0, 3 * OneSecondUs, 1, []);
        var project = new Project(Guid.NewGuid(), "Textos", new ProjectSettings(1920, 1080, ThirtyFps, 48_000))
            .AddAsset(title)
            .AddTrack(textTrack)
            .InsertClip(textTrack.Id, clip);
        var secondHalfId = Guid.NewGuid();

        var split = project.SplitClip(clip.Id, 30, secondHalfId);
        var secondHalfTextId = split.GetClip(secondHalfId).AssetId;
        var edited = split.UpdateText(secondHalfTextId, TextStyle.Default("Adiós"));

        Assert.NotEqual(clip.AssetId, secondHalfTextId);
        Assert.Equal("Hola", edited.GetAsset(clip.AssetId).Text!.Content);
        Assert.Equal("Adiós", edited.GetAsset(secondHalfTextId).Text!.Content);
        Assert.DoesNotContain(edited.RemoveClip(secondHalfId).Assets, asset => asset.Id == secondHalfTextId);
    }

    [Theory]
    [InlineData("", 0.08, "#FFFFFF")]
    [InlineData("Hola", 0.9, "#FFFFFF")]
    [InlineData("Hola", 0.08, "blanco")]
    public void Invalid_text_styles_are_rejected(string content, double size, string color)
    {
        Assert.Throws<ValidationException>(() => new TextStyle(content, size, color, hasBackground: false, isBold: false));
    }

    [Fact]
    public void Rotating_cycles_through_quarter_turns_and_returns_upright()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);

        var ninety = project.RotateClipQuarterTurn(clip.Id);
        Assert.Equal(90, ninety.GetClip(clip.Id).FindEffect(EffectCatalog.Rotate)!.Parameters[EffectCatalog.Degrees]);

        var upright = ninety
            .RotateClipQuarterTurn(clip.Id)
            .RotateClipQuarterTurn(clip.Id)
            .RotateClipQuarterTurn(clip.Id);
        Assert.Null(upright.GetClip(clip.Id).FindEffect(EffectCatalog.Rotate));
    }

    [Fact]
    public void Freezing_splits_inserts_a_still_and_ripples_the_rest_right()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var image = MediaAsset.StillImage(Guid.NewGuid(), @"C:\media\congelado.png", 1920, 1080);

        var frozen = project.FreezeFrame(clip.Id, atFrame: 30, image, Guid.NewGuid(), Guid.NewGuid(), holdFrames: 30);

        var clips = frozen.GetTrack(VideoTrack.Id).Clips;
        Assert.Equal(3, clips.Count);
        Assert.Equal(30, clips[0].TimelineEndFrame(ThirtyFps));                       // primera mitad: 0..30
        Assert.Equal((30L, image.Id), (clips[1].TimelineStartFrame, clips[1].AssetId)); // congelado: 30..60
        Assert.Equal(60, clips[1].TimelineEndFrame(ThirtyFps));
        Assert.Equal(60, clips[2].TimelineStartFrame);                                // segunda mitad desplazada: 60..90
        Assert.Equal(MediaKind.Image, frozen.GetAsset(clips[1].AssetId).Kind);
    }

    [Fact]
    public void Freezing_outside_the_clip_is_rejected()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var image = MediaAsset.StillImage(Guid.NewGuid(), @"C:\media\x.png", 100, 100);

        Assert.Throws<BusinessRuleException>(() => project.FreezeFrame(clip.Id, atFrame: 0, image, Guid.NewGuid(), Guid.NewGuid(), holdFrames: 30));
    }

    [Fact]
    public void A_locked_track_refuses_clip_edits_until_it_is_unlocked()
    {
        var (project, clip) = ProjectWithClip(Video, VideoTrack);
        var locked = project.SetTrackLocked(VideoTrack.Id, isLocked: true);

        Assert.True(locked.GetTrack(VideoTrack.Id).IsLocked);
        Assert.Throws<BusinessRuleException>(() => locked.RemoveClip(clip.Id));
        Assert.Throws<BusinessRuleException>(() => locked.SetClipSpeed(clip.Id, 2));
        Assert.Throws<BusinessRuleException>(() => locked.ShiftClip(clip.Id, 5));

        var unlocked = locked.SetTrackLocked(VideoTrack.Id, isLocked: false);
        Assert.Equal(2, unlocked.SetClipSpeed(clip.Id, 2).GetClip(clip.Id).Speed);
    }

    [Fact]
    public void Text_only_goes_on_text_tracks()
    {
        var title = MediaAsset.CreateText(Guid.NewGuid(), TextStyle.Default("Hola"));
        var (project, _) = ProjectWithClip(Video, VideoTrack);

        Assert.Throws<BusinessRuleException>(() =>
            project.AddAsset(title).InsertClip(VideoTrack.Id, new Clip(Guid.NewGuid(), title.Id, 100, 0, OneSecondUs, 1, [])));
    }
}
