using System.Collections.Generic;

namespace Vineeeeea.App.Utils;

public sealed record TextColorPreset(string Name, string Hex)
{
    public override string ToString() => Name;
}

public static class TextColors
{
    public static readonly IReadOnlyList<TextColorPreset> All =
    [
        new("Blanco", "#FFFFFF"),
        new("Negro", "#000000"),
        new("Amarillo", "#FFD60A"),
        new("Rojo", "#FF3B30"),
        new("Azul", "#0A84FF"),
        new("Verde", "#30D158"),
        new("Rosa", "#FF2D92"),
    ];
}
