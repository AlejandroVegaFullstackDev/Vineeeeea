using System.Collections.Immutable;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Domain.Models;

public sealed record EffectInstance
{
    public Guid Id { get; }
    public string Type { get; }
    public bool Enabled { get; }
    public ImmutableSortedDictionary<string, double> Parameters { get; }
    public ImmutableSortedDictionary<string, ImmutableList<Keyframe>> Keyframes { get; }

    public EffectInstance(
        Guid id,
        string type,
        bool enabled,
        IReadOnlyDictionary<string, double> parameters,
        IReadOnlyDictionary<string, ImmutableList<Keyframe>>? keyframes = null)
    {
        var spec = EffectCatalog.Get(type);
        foreach (var (name, value) in parameters)
        {
            EnsureInRange(spec, spec.GetParameter(name), value);
        }

        var keyframesByParameter = ImmutableSortedDictionary.CreateBuilder<string, ImmutableList<Keyframe>>(StringComparer.Ordinal);
        foreach (var (name, parameterKeyframes) in keyframes ?? ImmutableSortedDictionary<string, ImmutableList<Keyframe>>.Empty)
        {
            var parameter = spec.GetParameter(name);
            if (!parameter.Keyframable)
            {
                throw new ValidationException($"El parámetro {name} de {type} no admite keyframes.");
            }

            var ordered = parameterKeyframes.OrderBy(keyframe => keyframe.SourceUs).ToImmutableList();
            if (ordered.Select(keyframe => keyframe.SourceUs).Distinct().Count() != ordered.Count)
            {
                throw new ValidationException($"Hay dos keyframes de {name} en el mismo instante.");
            }

            foreach (var keyframe in ordered)
            {
                EnsureInRange(spec, parameter, keyframe.Value);
            }

            if (!ordered.IsEmpty)
            {
                keyframesByParameter[name] = ordered;
            }
        }

        Id = NameRules.RequireId(id, "El efecto");
        Type = spec.Type;
        Enabled = enabled;
        Parameters = spec.Parameters.ToImmutableSortedDictionary(
            parameter => parameter.Name,
            parameter => parameters.TryGetValue(parameter.Name, out var value) ? value : parameter.Default,
            StringComparer.Ordinal);
        Keyframes = keyframesByParameter.ToImmutable();
    }

    public static EffectInstance CreateDefault(string type) =>
        new(Guid.NewGuid(), type, enabled: true, ImmutableSortedDictionary<string, double>.Empty);

    public EffectSpec Spec => EffectCatalog.Get(Type);

    public double ValueAt(string parameter, long sourceUs)
    {
        if (!Keyframes.TryGetValue(parameter, out var keyframes))
        {
            return Parameters[parameter];
        }

        if (sourceUs <= keyframes[0].SourceUs)
        {
            return keyframes[0].Value;
        }

        for (var index = 1; index < keyframes.Count; index++)
        {
            var (previous, next) = (keyframes[index - 1], keyframes[index]);
            if (sourceUs <= next.SourceUs)
            {
                var progress = (double)(sourceUs - previous.SourceUs) / (next.SourceUs - previous.SourceUs);
                return previous.Value + (next.Value - previous.Value) * progress;
            }
        }

        return keyframes[^1].Value;
    }

    public EffectInstance WithEnabled(bool enabled) => new(Id, Type, enabled, Parameters, Keyframes);

    public EffectInstance WithParameter(string parameter, double value) =>
        new(Id, Type, Enabled, Parameters.SetItem(parameter, value), Keyframes);

    public EffectInstance WithKeyframe(string parameter, long sourceUs, double value)
    {
        var existing = Keyframes.GetValueOrDefault(parameter, []);
        var updated = existing.RemoveAll(keyframe => keyframe.SourceUs == sourceUs).Add(new Keyframe(sourceUs, value));
        return new EffectInstance(Id, Type, Enabled, Parameters, Keyframes.SetItem(parameter, updated));
    }

    public EffectInstance WithoutKeyframes(string parameter) =>
        new(Id, Type, Enabled, Parameters, Keyframes.Remove(parameter));

    private static void EnsureInRange(EffectSpec spec, EffectParameterSpec parameter, double value)
    {
        if (!double.IsFinite(value) || value < parameter.Min || value > parameter.Max)
        {
            throw new ValidationException(
                $"{spec.Type}.{parameter.Name} debe estar entre {parameter.Min} y {parameter.Max}.");
        }
    }
}
