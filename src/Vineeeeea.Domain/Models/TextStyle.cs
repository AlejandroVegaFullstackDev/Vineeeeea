using System.Text.RegularExpressions;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Domain.Models;

public sealed partial record TextStyle
{
    public string Content { get; }

    /// <summary>Letter height as a fraction of the canvas height, so text looks the same in preview and export.</summary>
    public double Size { get; }

    public string Color { get; }
    public bool HasBackground { get; }
    public bool IsBold { get; }

    public TextStyle(string content, double size, string color, bool hasBackground, bool isBold)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > DomainLimits.MaxTextLength)
        {
            throw new ValidationException($"El texto debe tener entre 1 y {DomainLimits.MaxTextLength} caracteres.");
        }

        if (!double.IsFinite(size) || size < DomainLimits.MinTextSize || size > DomainLimits.MaxTextSize)
        {
            throw new ValidationException("El tamaño del texto está fuera de rango.");
        }

        if (!HexColor().IsMatch(color))
        {
            throw new ValidationException("El color del texto debe tener el formato #RRGGBB.");
        }

        Content = content;
        Size = size;
        Color = color.ToUpperInvariant();
        HasBackground = hasBackground;
        IsBold = isBold;
    }

    public static TextStyle Default(string content) =>
        new(content, DomainLimits.DefaultTextSize, DomainLimits.DefaultTextColor, hasBackground: false, isBold: true);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
