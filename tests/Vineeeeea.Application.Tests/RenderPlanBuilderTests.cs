using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Application.Tests;

public class RenderPlanBuilderTests
{
    private const long TenSecondsUs = 10_000_000;
    private const long TwoSecondsUs = 2_000_000;

    private static readonly FrameSize Size = new(640, 360);
    private static readonly MediaAsset Video = new(Guid.NewGuid(), MediaKind.Video, @"C:\media\a.mp4", TenSecondsUs, 1920, 1080, hasAudio: true);
    private static readonly MediaAsset Music = new(Guid.NewGuid(), MediaKind.Audio, @"C:\media\m.mp3", TenSecondsUs, null, null, hasAudio: true);

    private static Project NewProject(bool hideOverlay = false, bool muteMusic = false)
    {
        var main = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var overlay = new Track(Guid.NewGuid(), TrackKind.Video, "Video 2", isMuted: false, isHidden: hideOverlay);
        var music = new Track(Guid.NewGuid(), TrackKind.Audio, "Audio 1", isMuted: muteMusic, isHidden: false);
        return new Project(Guid.NewGuid(), "Plan", new ProjectSettings(1920, 1080, new FrameRate(30, 1), 48_000))
            .AddAsset(Video)
            .AddAsset(Music)
            .AddTrack(main)
            .AddTrack(overlay)
            .AddTrack(music)
            .InsertClip(main.Id, new Clip(Guid.NewGuid(), Video.Id, 0, 0, TwoSecondsUs, 1, []))
            .InsertClip(overlay.Id, new Clip(Guid.NewGuid(), Video.Id, 30, TwoSecondsUs, TwoSecondsUs, 2, []))
            .InsertClip(music.Id, new Clip(Guid.NewGuid(), Music.Id, 0, 0, TenSecondsUs, 1, [EffectInstance.CreateDefault(EffectCatalog.Volume).WithEnabled(false)]));
    }

    [Fact]
    public void Layers_follow_track_order_with_their_roles()
    {
        var plan = RenderPlanBuilder.Build(NewProject(), 0, 300, Size);

        Assert.Equal([true, true, false], plan.Layers.Select(layer => layer.ProducesVideo));
        Assert.All(plan.Layers, layer => Assert.True(layer.ProducesAudio));
        Assert.Equal(300, plan.DurationFrames);
    }

    [Fact]
    public void A_range_in_the_middle_trims_layers_to_it()
    {
        var plan = RenderPlanBuilder.Build(NewProject(), 45, 46, Size);

        Assert.Equal(3, plan.Layers.Count);
        var overlay = plan.Layers[1];
        Assert.Equal((0L, 1L), (overlay.StartFrame, overlay.DurationFrames));
        Assert.Equal(3_000_000, overlay.SourceStartUs);
        Assert.Equal(66_666, overlay.SourceDurationUs);
    }

    [Fact]
    public void Hidden_tracks_keep_their_sound_and_muted_tracks_are_skipped()
    {
        var plan = RenderPlanBuilder.Build(NewProject(hideOverlay: true, muteMusic: true), 0, 300, Size);

        Assert.Equal(2, plan.Layers.Count);
        Assert.False(plan.Layers[1].ProducesVideo);
        Assert.True(plan.Layers[1].ProducesAudio);
    }

    [Fact]
    public void Disabled_effects_are_not_rendered()
    {
        var plan = RenderPlanBuilder.Build(NewProject(), 0, 300, Size);

        Assert.Empty(plan.Layers[2].Effects);
    }

    [Fact]
    public void An_empty_range_is_rejected()
    {
        Assert.Throws<BusinessRuleException>(() => RenderPlanBuilder.Build(NewProject(), 10, 10, Size));
    }

    [Fact]
    public void A_dissolve_draws_the_incoming_clip_early_over_its_neighbour_without_moving_its_sound()
    {
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var incoming = new Clip(Guid.NewGuid(), Video.Id, 60, 2 * TwoSecondsUs, TwoSecondsUs, 1,
            [EffectInstance.CreateDefault(EffectCatalog.Dissolve).WithParameter(EffectCatalog.Seconds, 1)]);
        var project = new Project(Guid.NewGuid(), "Disolver", new ProjectSettings(1920, 1080, new FrameRate(30, 1), 48_000))
            .AddAsset(Video)
            .AddTrack(track)
            .InsertClip(track.Id, new Clip(Guid.NewGuid(), Video.Id, 0, 0, TwoSecondsUs, 1, []))
            .InsertClip(track.Id, incoming);

        var layers = RenderPlanBuilder.Build(project, 0, 120, Size).Layers;

        var incomingVideo = layers.Single(layer => layer.ProducesVideo && layer.StartFrame == 30);
        var incomingAudio = layers.Single(layer => !layer.ProducesVideo && layer.ProducesAudio);
        Assert.Equal(3_000_000, incomingVideo.SourceStartUs);
        Assert.Equal(1_000_000, incomingVideo.VideoRamp!.InUs);
        Assert.Equal(60, incomingAudio.StartFrame);
    }

    [Fact]
    public void Without_a_neighbour_a_dissolve_acts_as_a_fade_in()
    {
        var track = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: false);
        var lonely = new Clip(Guid.NewGuid(), Video.Id, 30, 0, TwoSecondsUs, 1,
            [EffectInstance.CreateDefault(EffectCatalog.Dissolve).WithParameter(EffectCatalog.Seconds, 0.5)]);
        var project = new Project(Guid.NewGuid(), "Solo", new ProjectSettings(1920, 1080, new FrameRate(30, 1), 48_000))
            .AddAsset(Video)
            .AddTrack(track)
            .InsertClip(track.Id, lonely);

        var layer = Assert.Single(RenderPlanBuilder.Build(project, 0, 120, Size).Layers);

        Assert.Equal(30, layer.StartFrame);
        Assert.Equal(500_000, layer.VideoRamp!.InUs);
    }
}
