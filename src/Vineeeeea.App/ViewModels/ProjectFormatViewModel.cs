using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Vineeeeea.App.Utils;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;

namespace Vineeeeea.App.ViewModels;

public sealed partial class ProjectFormatViewModel(
    EditorSession session,
    ProjectSettingsController settings,
    Action<string> reportStatus) : ObservableObject
{
    private bool showingModel;

    public IReadOnlyList<CanvasPreset> Canvases => FormatPresets.Canvases;
    public IReadOnlyList<FrameRatePreset> FrameRates => FormatPresets.FrameRates;

    [ObservableProperty]
    public partial CanvasPreset? SelectedCanvas { get; set; }

    [ObservableProperty]
    public partial FrameRatePreset? SelectedFrameRate { get; set; }

    [ObservableProperty]
    public partial bool HasProject { get; private set; }

    public void Refresh()
    {
        var project = session.Project;
        showingModel = true;
        HasProject = project is not null;
        SelectedCanvas = Canvases.FirstOrDefault(preset =>
            preset.Width == project?.Settings.Width && preset.Height == project.Settings.Height);
        SelectedFrameRate = FrameRates.FirstOrDefault(preset => preset.FrameRate == project?.Settings.FrameRate);
        showingModel = false;
    }

    partial void OnSelectedCanvasChanged(CanvasPreset? value)
    {
        if (!showingModel && value is not null)
        {
            Run(() => settings.SetCanvas(value.Width, value.Height), $"Formato cambiado a {value.Name}.");
        }
    }

    partial void OnSelectedFrameRateChanged(FrameRatePreset? value)
    {
        if (!showingModel && value is not null)
        {
            Run(() => settings.SetFrameRate(value.FrameRate), $"Fotogramas por segundo: {value.Name}.");
        }
    }

    private void Run(Action edit, string successMessage)
    {
        try
        {
            edit();
            reportStatus(successMessage);
        }
        catch (VineeeeeaException exception)
        {
            reportStatus(exception.Message);
        }
    }
}
