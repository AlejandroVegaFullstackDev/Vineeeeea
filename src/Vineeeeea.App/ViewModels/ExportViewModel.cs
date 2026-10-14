using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vineeeeea.App.Services;
using Vineeeeea.App.Utils;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;

namespace Vineeeeea.App.ViewModels;

public sealed partial class DestinationOptionViewModel(ExportDestination destination) : ObservableObject
{
    public ExportDestination Destination { get; } = destination;
    public string Title { get; } = destination.Title;
    public string Description { get; } = destination.Description;
    public string Spec { get; } = destination.Spec;
    public bool IsLandscape => Destination.IsLandscape;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed partial class ExportViewModel : ObservableObject
{
    private readonly ExportController export;
    private readonly FileDialogService dialogs;
    private readonly EditorSession session;
    private readonly Action back;
    private readonly Action<string> reportStatus;
    private CancellationTokenSource? run;
    private string? lastOutputPath;

    public ExportViewModel(
        ExportController export,
        FileDialogService dialogs,
        EditorSession session,
        Action back,
        Action<string> reportStatus)
    {
        this.export = export;
        this.dialogs = dialogs;
        this.session = session;
        this.back = back;
        this.reportStatus = reportStatus;
        Destinations = new ObservableCollection<DestinationOptionViewModel>(
            ExportDestinations.All.Select(destination => new DestinationOptionViewModel(destination)));
        Destinations[0].IsSelected = true;
        FolderPath = DefaultFolder();
    }

    public ObservableCollection<DestinationOptionViewModel> Destinations { get; }

    [ObservableProperty]
    public partial string ProjectSummary { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FolderPath { get; set; }

    [ObservableProperty]
    public partial string? LetterboxNotice { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    public partial double Progress { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand), nameof(CancelCommand))]
    public partial bool IsExporting { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFinished))]
    public partial string? FinishedMessage { get; private set; }

    public bool HasFinished => FinishedMessage is not null;
    public int ProgressPercent => (int)Math.Round(Progress * 100);

    private DestinationOptionViewModel Selected => Destinations.First(option => option.IsSelected);

    /// <summary>Refreshes the summary and defaults each time the screen is shown.</summary>
    public void Prepare()
    {
        var project = session.Project;
        if (project is null)
        {
            return;
        }

        var frameRate = project.Settings.FrameRate;
        var shape = project.Settings.Width == project.Settings.Height ? "cuadrado"
            : project.Settings.Width > project.Settings.Height ? "horizontal" : "vertical";
        ProjectSummary = string.Join(" · ",
            project.Name,
            TimeFormatting.FormatMicroseconds(frameRate.FramesToMicroseconds(project.EndFrame())),
            shape);
        if (string.IsNullOrWhiteSpace(FileName))
        {
            FileName = project.Name;
        }

        FinishedMessage = null;
        RefreshLetterbox();
    }

    [RelayCommand]
    private void SelectDestination(DestinationOptionViewModel chosen)
    {
        foreach (var option in Destinations)
        {
            option.IsSelected = option == chosen;
        }

        RefreshLetterbox();
    }

    [RelayCommand]
    private async Task ChangeFolderAsync()
    {
        if (await dialogs.PickFolderAsync(Directory.Exists(FolderPath) ? FolderPath : null) is { } folder)
        {
            FolderPath = folder;
        }
    }

    [RelayCommand]
    private void Back() => back();

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var destination = Selected.Destination;
        string outputPath;
        try
        {
            outputPath = BuildOutputPath();
        }
        catch (VineeeeeaException exception)
        {
            reportStatus(exception.Message);
            return;
        }

        using var cancellation = run = new CancellationTokenSource();
        IsExporting = true;
        Progress = 0;
        FinishedMessage = null;
        var progress = new Progress<double>(value => Progress = value);
        try
        {
            await export.ExportAsync(destination, outputPath, progress, cancellation.Token);
            lastOutputPath = outputPath;
            FinishedMessage = $"¡Listo! Guardado como {Path.GetFileName(outputPath)}.";
            reportStatus("Video exportado.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            reportStatus("Exportación cancelada.");
        }
        catch (VineeeeeaException exception)
        {
            reportStatus(exception.Message);
        }
        finally
        {
            run = null;
            IsExporting = false;
        }
    }

    private bool CanExport() => !IsExporting;

    [RelayCommand(CanExecute = nameof(IsExporting))]
    private void Cancel() => run?.Cancel();

    [RelayCommand(CanExecute = nameof(HasFinished))]
    private Task OpenFolder() =>
        lastOutputPath is null ? Task.CompletedTask : dialogs.OpenContainingFolderAsync(lastOutputPath);

    partial void OnFinishedMessageChanged(string? value) => OpenFolderCommand.NotifyCanExecuteChanged();

    private void RefreshLetterbox()
    {
        if (session.Project is null)
        {
            LetterboxNotice = null;
            return;
        }

        LetterboxNotice = export.WillLetterbox(Selected.Destination)
            ? Selected.IsLandscape
                ? "Tu video no es horizontal: quedará centrado con franjas a los lados."
                : "Tu video no es vertical: quedará centrado con franjas arriba y abajo."
            : null;
    }

    private string BuildOutputPath()
    {
        var name = FileName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ValidationException("Escribe un nombre para el archivo.");
        }

        if (!Directory.Exists(FolderPath))
        {
            throw new ValidationException("Elige una carpeta donde guardar el video.");
        }

        if (!name.EndsWith(FileConventions.ExportExtension, StringComparison.OrdinalIgnoreCase))
        {
            name += FileConventions.ExportExtension;
        }

        return Path.Combine(FolderPath, name);
    }

    private static string DefaultFolder()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        return string.IsNullOrEmpty(videos) ? Environment.CurrentDirectory : videos;
    }
}
