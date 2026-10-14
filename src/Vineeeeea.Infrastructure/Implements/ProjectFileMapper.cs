using System.Collections.Immutable;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Infrastructure.Dtos;

namespace Vineeeeea.Infrastructure.Implements;

internal static class ProjectFileMapper
{
    public static ProjectFileDto ToDto(Project project, string projectDirectory) => new(
        FileConventions.FormatId,
        FileConventions.CurrentSchemaVersion,
        project.Id,
        project.Name,
        new ProjectSettingsDto(
            project.Settings.Width,
            project.Settings.Height,
            project.Settings.FrameRate.Numerator,
            project.Settings.FrameRate.Denominator,
            project.Settings.AudioSampleRate),
        project.Assets
            .Select(asset => new MediaAssetDto(
                asset.Id,
                asset.Kind,
                asset.IsFile ? ToStoredPath(asset.Path, projectDirectory) : string.Empty,
                asset.DurationUs,
                asset.Width,
                asset.Height,
                asset.HasAudio,
                asset.Text is { } text ? new TextStyleDto(text.Content, text.Size, text.Color, text.HasBackground, text.IsBold) : null))
            .ToList(),
        project.Tracks
            .Select(track => new TrackDto(
                track.Id,
                track.Kind,
                track.Name,
                track.IsMuted,
                track.IsHidden,
                track.Clips.Select(ToClipDto).ToList(),
                track.IsLocked))
            .ToList());

    public static Project ToDomain(ProjectFileDto dto, string projectDirectory)
    {
        if (dto.Format != FileConventions.FormatId || dto.SchemaVersion < FileConventions.OldestSupportedSchemaVersion)
        {
            throw new ValidationException("El archivo no es un proyecto de Vineeeeea.");
        }

        if (dto.SchemaVersion > FileConventions.CurrentSchemaVersion)
        {
            throw new BusinessRuleException("El proyecto se creó con una versión más reciente de Vineeeeea.");
        }

        var settings = new ProjectSettings(
            dto.Settings.Width,
            dto.Settings.Height,
            new FrameRate(dto.Settings.FrameRateNumerator, dto.Settings.FrameRateDenominator),
            dto.Settings.AudioSampleRate);

        var project = new Project(dto.Id, dto.Name, settings);
        foreach (var asset in dto.Assets)
        {
            project = project.AddAsset(ToAsset(asset, projectDirectory));
        }

        foreach (var track in dto.Tracks)
        {
            project = project.AddTrack(new Track(track.Id, track.Kind, track.Name, track.IsMuted, track.IsHidden));
            foreach (var clip in track.Clips)
            {
                project = project.InsertClip(track.Id, ToClip(clip));
            }

            if (track.IsLocked)
            {
                project = project.SetTrackLocked(track.Id, isLocked: true);
            }
        }

        return project;
    }

    private static MediaAsset ToAsset(MediaAssetDto asset, string projectDirectory)
    {
        if (asset.Kind == MediaKind.Text)
        {
            var text = asset.Text ?? throw new ValidationException("Un texto del proyecto no tiene contenido.");
            return MediaAsset.CreateText(asset.Id, new TextStyle(text.Content, text.Size, text.Color, text.HasBackground, text.IsBold));
        }

        return new MediaAsset(
            asset.Id,
            asset.Kind,
            ToAbsolutePath(asset.Path, projectDirectory),
            asset.DurationUs,
            asset.Width,
            asset.Height,
            asset.HasAudio);
    }

    private static ClipDto ToClipDto(Clip clip) => new(
        clip.Id,
        clip.AssetId,
        clip.TimelineStartFrame,
        clip.SourceInUs,
        clip.SourceDurationUs,
        clip.Speed,
        clip.Effects
            .Select(effect => new EffectDto(
                effect.Id,
                effect.Type,
                effect.Enabled,
                effect.Parameters,
                effect.Keyframes.ToDictionary(
                    entry => entry.Key,
                    IReadOnlyList<KeyframeDto> (entry) => entry.Value.Select(keyframe => new KeyframeDto(keyframe.SourceUs, keyframe.Value)).ToList())))
            .ToList());

    private static Clip ToClip(ClipDto clip) => new(
        clip.Id,
        clip.AssetId,
        clip.TimelineStartFrame,
        clip.SourceInUs,
        clip.SourceDurationUs,
        clip.Speed,
        clip.Effects.Select(effect => new EffectInstance(
            effect.Id,
            effect.Type,
            effect.Enabled,
            effect.Parameters,
            effect.Keyframes?.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Select(keyframe => new Keyframe(keyframe.SourceUs, keyframe.Value)).ToImmutableList()))));

    private static string ToStoredPath(string absolutePath, string projectDirectory)
    {
        var relativePath = Path.GetRelativePath(projectDirectory, absolutePath);
        var isInsideProject = !Path.IsPathRooted(relativePath) && !relativePath.StartsWith("..", StringComparison.Ordinal);
        return isInsideProject ? relativePath : absolutePath;
    }

    private static string ToAbsolutePath(string storedPath, string projectDirectory) =>
        Path.GetFullPath(Path.Combine(projectDirectory, storedPath));
}
