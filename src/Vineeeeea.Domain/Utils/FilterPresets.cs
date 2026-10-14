using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using static Vineeeeea.Domain.Utils.EffectCatalog;

namespace Vineeeeea.Domain.Utils;

public static class FilterPresets
{
    /// <summary>A filter replaces every color adjustment of the clip, so presets never stack on top of each other.</summary>
    public static readonly IReadOnlyList<string> ColorEffectTypes = [Brightness, Contrast, Saturation, Temperature, Grayscale, Vignette];

    public static readonly IReadOnlyList<FilterPreset> All =
    [
        new("Cálido", [new(Temperature, Kelvin, 4800), new(Saturation, Amount, 1.15)]),
        new("Frío", [new(Temperature, Kelvin, 8800), new(Contrast, Amount, 1.05)]),
        new("Vívido", [new(Saturation, Amount, 1.5), new(Contrast, Amount, 1.15)]),
        new("Vintage", [new(Saturation, Amount, 0.65), new(Temperature, Kelvin, 5200), new(Vignette, Amount, 0.6), new(Contrast, Amount, 1.1)]),
        new("Blanco y negro", [new(Grayscale, Amount, 1), new(Contrast, Amount, 1.15)]),
        new("Cine", [new(Contrast, Amount, 1.2), new(Saturation, Amount, 0.85), new(Vignette, Amount, 0.45), new(Brightness, Amount, -0.03)]),
        new("Pastel", [new(Saturation, Amount, 0.8), new(Contrast, Amount, 0.9), new(Brightness, Amount, 0.06), new(Temperature, Kelvin, 5600)]),
        new("Dramático", [new(Contrast, Amount, 1.45), new(Saturation, Amount, 1.1), new(Vignette, Amount, 0.6), new(Brightness, Amount, -0.05)]),
        new("Nocturno", [new(Temperature, Kelvin, 9500), new(Contrast, Amount, 1.15), new(Brightness, Amount, -0.12), new(Vignette, Amount, 0.5)]),
    ];

    public static FilterPreset Get(string name) =>
        All.FirstOrDefault(preset => preset.Name == name) ?? throw new ValidationException($"El filtro {name} no existe.");
}
