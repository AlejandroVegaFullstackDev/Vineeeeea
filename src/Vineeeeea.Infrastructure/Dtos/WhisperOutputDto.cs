using System.Text.Json.Serialization;

namespace Vineeeeea.Infrastructure.Dtos;

internal sealed record WhisperOutputDto(
    [property: JsonPropertyName("transcription")] IReadOnlyList<WhisperSegmentDto>? Transcription);

internal sealed record WhisperSegmentDto(
    [property: JsonPropertyName("offsets")] WhisperOffsetsDto? Offsets,
    [property: JsonPropertyName("text")] string? Text);

internal sealed record WhisperOffsetsDto(
    [property: JsonPropertyName("from")] long From,
    [property: JsonPropertyName("to")] long To);
