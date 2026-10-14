using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Application.Controllers;

public sealed class CaptionsController(EditorSession session, ISpeechTranscriber transcriber)
{
    private const double CaptionSize = 0.055;
    private const double CaptionVerticalPosition = 0.7;
    private const string CaptionTrackLabel = "Subtítulos";

    /// <summary>Transcribes the clip's sound and lays each phrase out as a caption on a new text track, in one undo step.</summary>
    public async Task<int> GenerateAsync(Guid clipId, CancellationToken cancellationToken)
    {
        var project = session.RequireProject();
        var clip = project.GetClip(clipId);
        var asset = project.GetAsset(clip.AssetId);
        if (!asset.IsFile || !asset.HasAudio)
        {
            throw new BusinessRuleException("Este clip no tiene sonido que transcribir.");
        }

        var segments = await transcriber.TranscribeAsync(asset.Path, clip.SourceInUs, clip.SourceDurationUs, cancellationToken);
        var phrases = segments.Where(segment => !string.IsNullOrWhiteSpace(segment.Text)).ToList();
        if (phrases.Count == 0)
        {
            throw new BusinessRuleException("No se detectó voz en el clip.");
        }

        session.Apply(current => AddCaptions(current, current.GetClip(clipId), phrases));
        return phrases.Count;
    }

    private static Project AddCaptions(Project project, Clip clip, IReadOnlyList<TranscriptSegment> phrases)
    {
        var frameRate = project.Settings.FrameRate;
        var captionNumber = project.Tracks.Count(track => track.Name.StartsWith(CaptionTrackLabel, StringComparison.Ordinal)) + 1;
        var track = new Track(Guid.NewGuid(), TrackKind.Text, $"{CaptionTrackLabel} {captionNumber}", isMuted: false, isHidden: false);
        var updated = project.AddTrack(track);
        var clipEnd = clip.TimelineEndFrame(frameRate);
        var previousEnd = 0L;

        foreach (var phrase in phrases)
        {
            var start = Math.Max(previousEnd, clip.TimelineStartFrame + frameRate.MicrosecondsToFramesFloor((long)(phrase.StartUs / clip.Speed)));
            var end = Math.Min(clipEnd, clip.TimelineStartFrame + frameRate.MicrosecondsToFramesFloor((long)(phrase.EndUs / clip.Speed)));
            if (end <= start)
            {
                continue;
            }

            var text = MediaAsset.CreateText(Guid.NewGuid(), CaptionStyle(phrase.Text));
            var position = EffectInstance.CreateDefault(EffectCatalog.Transform).WithParameter(EffectCatalog.PositionY, CaptionVerticalPosition);
            var caption = new Clip(Guid.NewGuid(), text.Id, start, 0, frameRate.FramesToMicroseconds(end - start), DomainLimits.NormalSpeed, [position]);
            updated = updated.AddAsset(text).InsertClip(track.Id, caption);
            previousEnd = end;
        }

        return updated;
    }

    private static TextStyle CaptionStyle(string phrase)
    {
        var content = phrase.Trim();
        return new TextStyle(
            content.Length > DomainLimits.MaxTextLength ? content[..DomainLimits.MaxTextLength] : content,
            CaptionSize,
            DomainLimits.DefaultTextColor,
            hasBackground: true,
            isBold: true);
    }
}
