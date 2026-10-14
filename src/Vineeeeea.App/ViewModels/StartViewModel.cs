using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vineeeeea.App.Utils;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.App.ViewModels;

public sealed partial class ShapeOptionViewModel(CanvasShape shape, string title, string hint) : ObservableObject
{
    public CanvasShape Shape { get; } = shape;
    public string Title { get; } = title;
    public string Hint { get; } = hint;
    public bool IsHorizontal => Shape == CanvasShape.Horizontal;
    public bool IsVertical => Shape == CanvasShape.Vertical;
    public bool IsSquare => Shape == CanvasShape.Square;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed record ChoiceOption<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

public sealed record RecentProjectViewModel(string Name, string Details, string FilePath);

public sealed partial class StartViewModel : ObservableObject
{
    private const string NewProjectName = "Mi video";
    private const int DefaultFramesPerSecond = 30;
    private const int DefaultAudioSampleRate = 48_000;

    private readonly ProjectFileController projectFiles;
    private readonly Func<string, Task> openProject;
    private readonly Func<Task> pickAndOpenProject;
    private readonly Action<string> reportStatus;

    public StartViewModel(
        ProjectFileController projectFiles,
        Func<string, Task> openProject,
        Func<Task> pickAndOpenProject,
        Action<string> reportStatus)
    {
        this.projectFiles = projectFiles;
        this.openProject = openProject;
        this.pickAndOpenProject = pickAndOpenProject;
        this.reportStatus = reportStatus;
        Shapes[0].IsSelected = true;
        SelectedFrameRate = FrameRates.Single(option => option.Value == DefaultFramesPerSecond);
        SelectedSampleRate = SampleRates.Single(option => option.Value == DefaultAudioSampleRate);
        RefreshRecents();
    }

    public IReadOnlyList<ShapeOptionViewModel> Shapes { get; } =
    [
        new(CanvasShape.Horizontal, "Horizontal", "YouTube, computador"),
        new(CanvasShape.Vertical, "Vertical", "TikTok, Reels, Shorts"),
        new(CanvasShape.Square, "Cuadrado", "Publicaciones de feed"),
    ];

    public IReadOnlyList<ChoiceOption<int>> FrameRates { get; } =
        [new("24", 24), new("25", 25), new("30", 30), new("60", 60)];

    public IReadOnlyList<ChoiceOption<int>> SampleRates { get; } =
        [new("44,1 kHz", 44_100), new("48 kHz", 48_000)];

    public ObservableCollection<RecentProjectViewModel> Recents { get; } = [];

    [ObservableProperty]
    public partial ChoiceOption<int> SelectedFrameRate { get; set; }

    [ObservableProperty]
    public partial ChoiceOption<int> SelectedSampleRate { get; set; }

    [ObservableProperty]
    public partial bool HasRecents { get; private set; }

    public CanvasShape SelectedShape => Shapes.First(option => option.IsSelected).Shape;

    public void RefreshRecents()
    {
        Recents.Clear();
        foreach (var recent in projectFiles.RecentProjects())
        {
            Recents.Add(new RecentProjectViewModel(
                recent.Name,
                $"{TimeFormatting.FormatSeconds(recent.DurationUs)} · {recent.FilePath}",
                recent.FilePath));
        }

        HasRecents = Recents.Count > 0;
    }

    /// <summary>Creates the project the user configured; returns false when the configuration was rejected.</summary>
    public bool CreateProject()
    {
        try
        {
            projectFiles.CreateNew(NewProjectName, SelectedShape, new FrameRate(SelectedFrameRate.Value, 1), SelectedSampleRate.Value);
            return true;
        }
        catch (VineeeeeaException exception)
        {
            reportStatus(exception.Message);
            return false;
        }
    }

    [RelayCommand]
    private void SelectShape(ShapeOptionViewModel chosen)
    {
        foreach (var option in Shapes)
        {
            option.IsSelected = option == chosen;
        }
    }

    [RelayCommand]
    private void Start() => CreateProject();

    [RelayCommand]
    private Task OpenProject() => pickAndOpenProject();

    [RelayCommand]
    private Task OpenRecent(RecentProjectViewModel recent) => openProject(recent.FilePath);
}
