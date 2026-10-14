namespace Vineeeeea.Domain.Models;

public sealed record EffectSetting(string EffectType, string Parameter, double Value);

public sealed record FilterPreset(string Name, IReadOnlyList<EffectSetting> Settings);
