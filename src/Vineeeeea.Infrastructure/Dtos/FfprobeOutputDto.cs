using System.Text.Json.Serialization;

namespace Vineeeeea.Infrastructure.Dtos;

internal sealed record FfprobeOutputDto(
    [property: JsonPropertyName("format")] FfprobeFormatDto? Format,
    [property: JsonPropertyName("streams")] IReadOnlyList<FfprobeStreamDto>? Streams);

internal sealed record FfprobeFormatDto(
    [property: JsonPropertyName("duration")] string? Duration,
    [property: JsonPropertyName("format_name")] string? FormatName);

internal sealed record FfprobeStreamDto(
    [property: JsonPropertyName("codec_type")] string? CodecType,
    [property: JsonPropertyName("codec_name")] string? CodecName,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height,
    [property: JsonPropertyName("disposition")] FfprobeDispositionDto? Disposition);

internal sealed record FfprobeDispositionDto(
    [property: JsonPropertyName("attached_pic")] int AttachedPicture);
