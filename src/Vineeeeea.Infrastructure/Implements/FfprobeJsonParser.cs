using System.Globalization;
using System.Text.Json;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Utils;
using Vineeeeea.Infrastructure.Dtos;

namespace Vineeeeea.Infrastructure.Implements;

internal static class FfprobeJsonParser
{
    public static ProbedMedia Parse(string json)
    {
        FfprobeOutputDto? output;
        try
        {
            output = JsonSerializer.Deserialize<FfprobeOutputDto>(json);
        }
        catch (JsonException)
        {
            throw new ExternalServiceException("ffprobe devolvió una respuesta que no se pudo interpretar.");
        }

        if (output is null)
        {
            throw new ExternalServiceException("ffprobe no devolvió información del archivo.");
        }

        var streams = (output.Streams ?? []).Select(ToStream).ToList();
        return new ProbedMedia(ParseDurationUs(output.Format?.Duration), streams, output.Format?.FormatName);
    }

    private static ProbedStream ToStream(FfprobeStreamDto stream) => new(
        stream.CodecType switch
        {
            "video" => ProbedStreamType.Video,
            "audio" => ProbedStreamType.Audio,
            _ => ProbedStreamType.Other,
        },
        stream.CodecName,
        stream.Width,
        stream.Height,
        stream.Disposition?.AttachedPicture == 1);

    private static long? ParseDurationUs(string? durationSeconds)
    {
        if (!decimal.TryParse(durationSeconds, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        return (long)Math.Round(seconds * TimeUnits.MicrosecondsPerSecond, MidpointRounding.AwayFromZero);
    }
}
