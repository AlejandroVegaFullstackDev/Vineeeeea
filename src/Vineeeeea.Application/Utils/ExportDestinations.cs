using Vineeeeea.Application.Config;
using Vineeeeea.Domain.Errors;

namespace Vineeeeea.Application.Utils;

/// <summary>
/// A place the finished video is meant for. The canvas is letterboxed into this size, so the project keeps its shape
/// and the destination keeps its own (a horizontal project exported to TikTok gets bars, and the reverse).
/// </summary>
public sealed record ExportDestination(
    string Id,
    string Title,
    string Description,
    int Width,
    int Height,
    ExportEncoding Encoding)
{
    public string Spec => $"{Width}×{Height} · MP4";
    public bool IsLandscape => Width >= Height;
}

public static class ExportDestinations
{
    public const string YouTube = "youtube";
    public const string TikTok = "tiktok";
    public const string WhatsApp = "whatsapp";
    public const string Best = "best";

    public static readonly IReadOnlyList<ExportDestination> All =
    [
        new(YouTube, "YouTube o computador", "Horizontal, se ve bien en pantalla grande.",
            1920, 1080, new ExportEncoding(VideoCrf: 20, VideoPreset: "medium", AudioBitrateKbps: 192)),
        new(TikTok, "TikTok, Reels o Shorts", "Vertical, para ver en el celular.",
            1080, 1920, new ExportEncoding(VideoCrf: 20, VideoPreset: "medium", AudioBitrateKbps: 192)),
        new(WhatsApp, "WhatsApp", "Archivo liviano que se envía rápido.",
            1280, 720, new ExportEncoding(VideoCrf: 28, VideoPreset: "veryfast", AudioBitrateKbps: 128)),
        new(Best, "Máxima calidad", "Para guardar o seguir editando en otro lado. Pesa más.",
            1920, 1080, new ExportEncoding(VideoCrf: 16, VideoPreset: "slow", AudioBitrateKbps: 256)),
    ];

    public static ExportDestination Get(string id) =>
        All.FirstOrDefault(destination => destination.Id == id)
        ?? throw new ValidationException($"El destino de exportación «{id}» no existe.");
}
