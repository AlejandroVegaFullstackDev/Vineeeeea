using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Vineeeeea.Application.Utils;

namespace Vineeeeea.App.Services;

public sealed class FileDialogService(IStorageProvider storageProvider, ILauncher launcher)
{
    private const string ImportedFolderName = "Vineeeeea-importados";

    private static readonly FilePickerFileType ProjectFileType = new("Proyecto de Vineeeeea")
    {
        Patterns = ["*" + FileConventions.ProjectExtension],
    };

    private static readonly FilePickerFileType ExportFileType = new("Video MP4")
    {
        Patterns = ["*" + FileConventions.ExportExtension],
    };

    private static readonly FilePickerFileType MediaFileType = new("Video, foto y audio")
    {
        Patterns =
        [
            "*.mp4", "*.mov", "*.mkv", "*.webm", "*.avi", "*.m4v", "*.gif",
            "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp",
            "*.mp3", "*.wav", "*.m4a", "*.aac", "*.flac", "*.ogg",
        ],
    };

    public async Task<IReadOnlyList<string>> PickMediaFilesAsync()
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importar medios",
            AllowMultiple = true,
            FileTypeFilter = [MediaFileType, FilePickerFileTypes.All],
        });

        var paths = new List<string>();
        foreach (var file in files)
        {
            if (await ToLocalPathAsync(file) is { } path)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    public async Task<string?> PickProjectToOpenAsync()
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Abrir proyecto",
            AllowMultiple = false,
            FileTypeFilter = [ProjectFileType],
        });

        return files.Count == 0 ? null : await ToLocalPathAsync(files[0]);
    }

    public async Task<string?> PickProjectSaveLocationAsync(string suggestedName)
    {
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Guardar proyecto",
            SuggestedFileName = suggestedName + FileConventions.ProjectExtension,
            DefaultExtension = FileConventions.ProjectExtension,
            FileTypeChoices = [ProjectFileType],
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickFolderAsync(string? startingFolder)
    {
        var options = new FolderPickerOpenOptions { Title = "Elegir carpeta", AllowMultiple = false };
        if (!string.IsNullOrWhiteSpace(startingFolder))
        {
            options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(startingFolder);
        }

        var folders = await storageProvider.OpenFolderPickerAsync(options);
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    /// <summary>Opens the folder that holds the given file in the system file explorer.</summary>
    public async Task OpenContainingFolderAsync(string filePath)
    {
        var folder = System.IO.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folder) && await storageProvider.TryGetFolderFromPathAsync(folder) is { } storageFolder)
        {
            await launcher.LaunchFileAsync(storageFolder);
        }
    }

    public async Task<string?> PickExportLocationAsync(string suggestedName)
    {
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar video",
            SuggestedFileName = suggestedName + FileConventions.ExportExtension,
            DefaultExtension = FileConventions.ExportExtension,
            FileTypeChoices = [ExportFileType],
        });

        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// Mobile pickers hand out content URIs with no file path: those are copied once into a local folder so ffmpeg and the project store can read them.
    /// </summary>
    private static async Task<string?> ToLocalPathAsync(IStorageFile file)
    {
        if (file.TryGetLocalPath() is { } localPath)
        {
            return localPath;
        }

        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), ImportedFolderName, Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var target = System.IO.Path.Combine(folder, string.IsNullOrWhiteSpace(file.Name) ? "medio" : file.Name);
        await using var source = await file.OpenReadAsync();
        await using var destination = System.IO.File.Create(target);
        await source.CopyToAsync(destination);
        return target;
    }
}
