using Vineeeeea.Domain.Errors;

namespace Vineeeeea.Domain.Models;

public sealed record EffectParameterSpec(string Name, double Min, double Max, double Default, bool Keyframable);

public sealed record EffectSpec(string Type, MediaKind Target, IReadOnlyList<EffectParameterSpec> Parameters)
{
    public EffectParameterSpec GetParameter(string name) =>
        Parameters.FirstOrDefault(parameter => parameter.Name == name)
        ?? throw new ValidationException($"El efecto {Type} no tiene el parámetro {name}.");

    public bool AppliesTo(MediaAsset asset) => Target switch
    {
        MediaKind.Video => asset.IsVisual,
        MediaKind.Audio => asset.HasAudio,
        _ => false,
    };
}
