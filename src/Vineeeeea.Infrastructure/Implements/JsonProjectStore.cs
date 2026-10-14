using System.Text.Json;
using System.Text.Json.Serialization;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Infrastructure.Dtos;

namespace Vineeeeea.Infrastructure.Implements;

public sealed class JsonProjectStore : IProjectStore
{
    private const long MaxProjectFileBytes = 50 * 1024 * 1024;
    private const string TemporarySuffix = ".tmp";
    private const string BackupSuffix = ".bak";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public async Task SaveAsync(Project project, string filePath, CancellationToken cancellationToken)
    {
        var dto = ProjectFileMapper.ToDto(project, ProjectDirectoryOf(filePath));
        var temporaryPath = filePath + TemporarySuffix;
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, filePath + BackupSuffix, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, filePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            File.Delete(temporaryPath);
            if (exception is OperationCanceledException)
            {
                throw;
            }

            throw new ExternalServiceException("No se pudo guardar el proyecto en esa ubicación.");
        }
    }

    public async Task<Project> LoadAsync(string filePath, CancellationToken cancellationToken)
    {
        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            throw new NotFoundException("No se encontró el archivo de proyecto.");
        }

        if (file.Length > MaxProjectFileBytes)
        {
            throw new ValidationException("El archivo de proyecto es demasiado grande.");
        }

        ProjectFileDto? dto;
        try
        {
            await using var stream = file.OpenRead();
            dto = await JsonSerializer.DeserializeAsync<ProjectFileDto>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            throw new ValidationException("El archivo de proyecto está dañado o tiene un formato no válido.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ExternalServiceException("No se pudo leer el archivo de proyecto.");
        }

        return ProjectFileMapper.ToDomain(
            dto ?? throw new ValidationException("El archivo de proyecto está vacío."),
            ProjectDirectoryOf(filePath));
    }

    private static string ProjectDirectoryOf(string filePath) =>
        Path.GetDirectoryName(filePath) ?? throw new ValidationException("La ruta del proyecto no tiene carpeta.");
}
