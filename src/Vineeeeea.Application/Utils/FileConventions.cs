using Vineeeeea.Domain.Errors;

namespace Vineeeeea.Application.Utils;

public static class FileConventions
{
    public const string ProjectExtension = ".vine";
    public const string ExportExtension = ".mp4";
    public const string FormatId = "vineeeeea-project";
    public const int CurrentSchemaVersion = 3;
    public const int OldestSupportedSchemaVersion = 1;

    public static string RequireProjectPath(string? filePath) => RequirePathWithExtension(filePath, ProjectExtension);

    public static string RequireExportPath(string? filePath) => RequirePathWithExtension(filePath, ExportExtension);

    public static string RequireAbsolutePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath))
        {
            throw new ValidationException("La ruta debe ser absoluta.");
        }

        return Path.GetFullPath(filePath);
    }

    private static string RequirePathWithExtension(string? filePath, string extension)
    {
        var fullPath = RequireAbsolutePath(filePath);
        if (!string.Equals(Path.GetExtension(fullPath), extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException($"El archivo debe tener la extensión {extension}.");
        }

        return fullPath;
    }
}
