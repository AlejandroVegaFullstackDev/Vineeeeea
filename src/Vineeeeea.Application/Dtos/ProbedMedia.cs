namespace Vineeeeea.Application.Dtos;

public sealed record ProbedMedia(long? DurationUs, IReadOnlyList<ProbedStream> Streams, string? FormatName = null);

public sealed record ProbedStream(
    ProbedStreamType Type,
    string? Codec,
    int? Width,
    int? Height,
    bool IsAttachedPicture);

public enum ProbedStreamType
{
    Video,
    Audio,
    Other,
}
