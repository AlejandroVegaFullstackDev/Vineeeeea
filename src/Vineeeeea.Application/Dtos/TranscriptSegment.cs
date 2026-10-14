namespace Vineeeeea.Application.Dtos;

/// <summary>A spoken phrase, timed relative to the start of the transcribed range of the source media.</summary>
public sealed record TranscriptSegment(long StartUs, long EndUs, string Text);
