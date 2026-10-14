namespace Vineeeeea.Infrastructure.Dtos;

internal sealed record TextRenderingContext(string FontPath, string BoldFontPath, Func<string, string> TextFileFor);
