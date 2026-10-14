using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Domain.Models;

public sealed record MediaAsset
{
    public Guid Id { get; private init; }
    public MediaKind Kind { get; }
    public string Path { get; }
    public long DurationUs { get; }
    public int? Width { get; }
    public int? Height { get; }
    public bool HasAudio { get; }
    public TextStyle? Text { get; private init; }

    public MediaAsset(Guid id, MediaKind kind, string path, long durationUs, int? width, int? height, bool hasAudio)
    {
        if (kind == MediaKind.Text)
        {
            throw new ValidationException("Un texto se crea con su estilo, no con una ruta.");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ValidationException("El medio necesita una ruta.");
        }

        if (durationUs <= 0)
        {
            throw new ValidationException("La duración del medio debe ser positiva.");
        }

        if ((kind is MediaKind.Video or MediaKind.Image) && (width is not > 0 || height is not > 0))
        {
            throw new ValidationException("Un medio visual necesita ancho y alto positivos.");
        }

        Id = NameRules.RequireId(id, "El medio");
        Kind = kind;
        Path = path;
        DurationUs = durationUs;
        Width = width;
        Height = height;
        HasAudio = hasAudio;
    }

    private MediaAsset(Guid id, TextStyle text)
    {
        Id = NameRules.RequireId(id, "El texto");
        Kind = MediaKind.Text;
        Path = string.Empty;
        DurationUs = DomainLimits.StillImageSourceUs;
        Text = text ?? throw new ValidationException("Falta el estilo del texto.");
    }

    public string DisplayName => Text is { } text ? FirstLine(text.Content) : System.IO.Path.GetFileName(Path);

    public bool IsVisual => Kind is MediaKind.Video or MediaKind.Image or MediaKind.Text;

    public bool IsFile => Kind != MediaKind.Text;

    public long DefaultClipDurationUs => Kind switch
    {
        MediaKind.Image => DomainLimits.DefaultImageClipUs,
        MediaKind.Text => DomainLimits.DefaultTextClipUs,
        _ => DurationUs,
    };

    public static MediaAsset CreateText(Guid id, TextStyle text) => new(id, text);

    internal MediaAsset WithText(TextStyle text) =>
        Kind == MediaKind.Text ? this with { Text = text } : throw new BusinessRuleException("Solo un texto tiene contenido editable.");

    internal MediaAsset CopyAs(Guid id) => this with { Id = NameRules.RequireId(id, "El medio") };

    private static string FirstLine(string content) => content.Split('\n', 2)[0].Trim();

    public static MediaAsset StillImage(Guid id, string path, int width, int height) =>
        new(id, MediaKind.Image, path, DomainLimits.StillImageSourceUs, width, height, hasAudio: false);
}
