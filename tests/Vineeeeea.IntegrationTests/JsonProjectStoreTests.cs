using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;
using Vineeeeea.Infrastructure.Implements;

namespace Vineeeeea.IntegrationTests;

public class JsonProjectStoreTests
{
    private readonly JsonProjectStore store = new();

    private static Project SampleProject(string mediaInsideProject, string mediaOutsideProject)
    {
        var video = new MediaAsset(Guid.NewGuid(), MediaKind.Video, mediaInsideProject, 5_000_000, 1920, 1080, hasAudio: true);
        var audio = new MediaAsset(Guid.NewGuid(), MediaKind.Audio, mediaOutsideProject, 8_000_000, null, null, hasAudio: true);
        var videoTrack = new Track(Guid.NewGuid(), TrackKind.Video, "Video 1", isMuted: false, isHidden: true);
        var audioTrack = new Track(Guid.NewGuid(), TrackKind.Audio, "Audio 1", isMuted: true, isHidden: false);
        var brightness = new EffectInstance(Guid.NewGuid(), EffectCatalog.Brightness, enabled: true, new Dictionary<string, double> { [EffectCatalog.Amount] = 0.25 })
            .WithKeyframe(EffectCatalog.Amount, 1_000_000, -0.5)
            .WithKeyframe(EffectCatalog.Amount, 2_500_000, 0.5);

        return new Project(Guid.NewGuid(), "Ida y vuelta", new ProjectSettings(1080, 1920, new FrameRate(30000, 1001), 48_000))
            .AddAsset(video)
            .AddAsset(audio)
            .AddTrack(videoTrack)
            .AddTrack(audioTrack)
            .InsertClip(videoTrack.Id, new Clip(Guid.NewGuid(), video.Id, 12, 1_000_000, 2_000_000, 1.5, [brightness]))
            .InsertClip(audioTrack.Id, new Clip(Guid.NewGuid(), audio.Id, 0, 0, 8_000_000, 1, []));
    }

    [Fact]
    public async Task Round_trip_preserves_ids_order_ranges_effects_and_settings()
    {
        using var directory = new TemporaryDirectory();
        var original = SampleProject(directory.Combine("media", "clip.mp4"), @"D:\musica\fondo.wav");
        var filePath = directory.Combine("demo.vine");

        await store.SaveAsync(original, filePath, CancellationToken.None);
        var loaded = await store.LoadAsync(filePath, CancellationToken.None);

        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal(original.Settings, loaded.Settings);
        Assert.Equal(original.Assets, loaded.Assets);
        Assert.Equal(original.Tracks.Select(track => (track.Id, track.Kind, track.Name, track.IsMuted, track.IsHidden, track.IsLocked)),
            loaded.Tracks.Select(track => (track.Id, track.Kind, track.Name, track.IsMuted, track.IsHidden, track.IsLocked)));

        var originalClip = original.Tracks[0].Clips[0];
        var loadedClip = loaded.Tracks[0].Clips[0];
        Assert.Equal(
            (originalClip.Id, originalClip.AssetId, originalClip.TimelineStartFrame, originalClip.SourceInUs, originalClip.SourceDurationUs, originalClip.Speed),
            (loadedClip.Id, loadedClip.AssetId, loadedClip.TimelineStartFrame, loadedClip.SourceInUs, loadedClip.SourceDurationUs, loadedClip.Speed));
        Assert.Equal(originalClip.Effects[0].Parameters, loadedClip.Effects[0].Parameters);
        Assert.Equal(originalClip.Effects[0].Keyframes[EffectCatalog.Amount], loadedClip.Effects[0].Keyframes[EffectCatalog.Amount]);
    }

    [Fact]
    public async Task Stores_media_inside_the_project_folder_as_relative_paths()
    {
        using var directory = new TemporaryDirectory();
        var filePath = directory.Combine("demo.vine");

        await store.SaveAsync(SampleProject(directory.Combine("media", "clip.mp4"), @"D:\musica\fondo.wav"), filePath, CancellationToken.None);
        var json = await File.ReadAllTextAsync(filePath);

        Assert.Contains("media/clip.mp4", json);
        Assert.DoesNotContain(directory.Path.Replace(@"\", @"\\"), json);
        Assert.Contains(@"D:\\musica\\fondo.wav", json);
    }

    [Fact]
    public async Task Second_save_keeps_a_backup_and_leaves_no_temporary_file()
    {
        using var directory = new TemporaryDirectory();
        var filePath = directory.Combine("demo.vine");
        var project = SampleProject(directory.Combine("clip.mp4"), @"D:\fondo.wav");

        await store.SaveAsync(project, filePath, CancellationToken.None);
        await store.SaveAsync(project.Rename("Renombrado"), filePath, CancellationToken.None);

        Assert.True(File.Exists(filePath + ".bak"));
        Assert.False(File.Exists(filePath + ".tmp"));
        Assert.Equal("Renombrado", (await store.LoadAsync(filePath, CancellationToken.None)).Name);
    }

    [Fact]
    public async Task Refuses_projects_from_a_newer_schema()
    {
        using var directory = new TemporaryDirectory();
        var filePath = directory.Combine("futuro.vine");
        await store.SaveAsync(SampleProject(directory.Combine("clip.mp4"), @"D:\fondo.wav"), filePath, CancellationToken.None);
        var json = await File.ReadAllTextAsync(filePath);
        await File.WriteAllTextAsync(filePath, json.Replace($"\"SchemaVersion\": {FileConventions.CurrentSchemaVersion}", "\"SchemaVersion\": 99"));

        await Assert.ThrowsAsync<BusinessRuleException>(() => store.LoadAsync(filePath, CancellationToken.None));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"Format\":\"vineeeeea-project\",\"SchemaVersion\":1}")]
    public async Task Rejects_corrupt_files(string content)
    {
        using var directory = new TemporaryDirectory();
        var filePath = directory.Combine("roto.vine");
        await File.WriteAllTextAsync(filePath, content);

        await Assert.ThrowsAsync<ValidationException>(() => store.LoadAsync(filePath, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_files_whose_rules_are_broken()
    {
        using var directory = new TemporaryDirectory();
        var filePath = directory.Combine("rango.vine");
        var project = SampleProject(directory.Combine("clip.mp4"), @"D:\fondo.wav");
        await store.SaveAsync(project, filePath, CancellationToken.None);
        var json = await File.ReadAllTextAsync(filePath);
        await File.WriteAllTextAsync(filePath, json.Replace("\"SourceDurationUs\": 2000000", "\"SourceDurationUs\": 9000000"));

        await Assert.ThrowsAsync<BusinessRuleException>(() => store.LoadAsync(filePath, CancellationToken.None));
    }

    [Fact]
    public async Task Opens_projects_saved_with_schema_version_1()
    {
        using var directory = new TemporaryDirectory();
        var filePath = directory.Combine("v1.vine");
        const string versionOneJson = """
            {
              "Format": "vineeeeea-project",
              "SchemaVersion": 1,
              "Id": "c5f1cc1e-942e-4a5e-aac0-2c22ad4d8a1c",
              "Name": "Antiguo",
              "Settings": { "Width": 1920, "Height": 1080, "FrameRateNumerator": 30, "FrameRateDenominator": 1, "AudioSampleRate": 48000 },
              "Assets": [ { "Id": "d1d5d0d4-8c22-4f85-a43f-91d59e05f2b9", "Kind": "Video", "Path": "clip.mp4", "DurationUs": 5000000, "Width": 1920, "Height": 1080, "HasAudio": true } ],
              "Tracks": [ { "Id": "df1d9e2b-38d9-4cb4-96cb-8f35c73b47de", "Kind": "Video", "Name": "Video 1", "IsMuted": false, "IsHidden": false,
                "Clips": [ { "Id": "c0ab920a-54c1-4d52-9652-6cc19e79d0c0", "AssetId": "d1d5d0d4-8c22-4f85-a43f-91d59e05f2b9", "TimelineStartFrame": 0,
                  "SourceInUs": 0, "SourceDurationUs": 2000000, "Speed": 1,
                  "Effects": [ { "Id": "0f8fad5b-d9cb-469f-a165-70867728950e", "Type": "brightness", "Enabled": true, "Parameters": { "amount": 0.1 } } ] } ] } ]
            }
            """;
        await File.WriteAllTextAsync(filePath, versionOneJson);

        var project = await store.LoadAsync(filePath, CancellationToken.None);

        var effect = Assert.Single(project.Tracks[0].Clips[0].Effects);
        Assert.Equal(0.1, effect.Parameters[EffectCatalog.Amount]);
        Assert.Empty(effect.Keyframes);
    }

    [Fact]
    public async Task Round_trips_a_locked_track()
    {
        using var directory = new TemporaryDirectory();
        var filePath = directory.Combine("bloqueada.vine");
        var project = SampleProject(directory.Combine("clip.mp4"), @"D:\fondo.wav");
        var locked = project.SetTrackLocked(project.Tracks[0].Id, isLocked: true);

        await store.SaveAsync(locked, filePath, CancellationToken.None);
        var loaded = await store.LoadAsync(filePath, CancellationToken.None);

        Assert.True(loaded.Tracks[0].IsLocked);
        Assert.False(loaded.Tracks[1].IsLocked);
    }

    [Fact]
    public async Task Round_trips_texts_with_their_style()
    {
        using var directory = new TemporaryDirectory();
        var filePath = directory.Combine("textos.vine");
        var title = MediaAsset.CreateText(Guid.NewGuid(), new TextStyle("Línea uno\nLínea dos", 0.12, "#FFD60A", hasBackground: true, isBold: false));
        var textTrack = new Track(Guid.NewGuid(), TrackKind.Text, "Texto 1", isMuted: false, isHidden: false);
        var project = new Project(Guid.NewGuid(), "Textos", new ProjectSettings(1080, 1920, new FrameRate(30, 1), 48_000))
            .AddAsset(title)
            .AddTrack(textTrack)
            .InsertClip(textTrack.Id, new Clip(Guid.NewGuid(), title.Id, 15, 0, 3_000_000, 1, []));

        await store.SaveAsync(project, filePath, CancellationToken.None);
        var loaded = await store.LoadAsync(filePath, CancellationToken.None);

        Assert.Equal(title.Text, loaded.GetAsset(title.Id).Text);
        Assert.Equal(TrackKind.Text, loaded.Tracks.Single().Kind);
    }
}
