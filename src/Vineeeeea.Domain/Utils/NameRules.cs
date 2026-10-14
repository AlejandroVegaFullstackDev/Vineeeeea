using Vineeeeea.Domain.Errors;

namespace Vineeeeea.Domain.Utils;

internal static class NameRules
{
    public static string Normalize(string? name, string fieldLabel)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ValidationException($"{fieldLabel} no puede estar vacío.");
        }

        if (trimmed.Length > DomainLimits.MaxNameLength)
        {
            throw new ValidationException($"{fieldLabel} supera {DomainLimits.MaxNameLength} caracteres.");
        }

        return trimmed;
    }

    public static Guid RequireId(Guid id, string fieldLabel) =>
        id == Guid.Empty ? throw new ValidationException($"{fieldLabel} necesita un identificador.") : id;
}
