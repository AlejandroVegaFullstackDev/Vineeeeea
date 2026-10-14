using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vineeeeea.App.Services;
using Vineeeeea.App.Utils;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Services;
using Vineeeeea.Application.Utils;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const string AppName = "Vineeeeea";
    private const string NewProjectName = "Proyecto sin título";
    private const int RulerLabelEverySeconds = 1;
    private const double MagnetThresholdPixels = 10;

    private readonly EditorSession session;
    private readonly ProjectFileController projectFiles;
    private readonly MediaImportController mediaImport;
    private readonly TimelineController timeline;
    private readonly MediaLibraryController mediaLibrary;
    private readonly HistoryController history;
    private readonly ProjectSettingsController projectSettings;
    private readonly ExportController export;
    private readonly ClipPreviewController clipPreviews;
    private readonly CaptionsController captions;
    private readonly FileDialogService dialogs;
    private readonly IUserPreferences preferences;
    private readonly Dictionary<Guid, Bitmap> thumbnails = [];
    private readonly Dictionary<Guid, Bitmap> waveforms = [];
    private readonly HashSet<Guid> requestedPreviews = [];

    public MainViewModel(
        EditorSession session,
        ProjectFileController projectFiles,
        MediaImportController mediaImport,
        TimelineController timeline,
        MediaLibraryController mediaLibrary,
        HistoryController history,
        ProjectSettingsController projectSettings,
        PlaybackController playback,
        ExportController export,
        ClipPreviewController clipPreviews,
        CaptionsController captions,
        FileDialogService dialogs,
        IUserPreferences preferences)
    {
        this.session = session;
        this.projectFiles = projectFiles;
        this.mediaImport = mediaImport;
        this.timeline = timeline;
        this.mediaLibrary = mediaLibrary;
        this.history = history;
        this.export = export;
        this.clipPreviews = clipPreviews;
        this.captions = captions;
        this.dialogs = dialogs;
        this.preferences = preferences;
        TimelineTipVisible = !preferences.TimelineTipDismissed;
        Player = new PlayerViewModel(session, playback, ReportStatus);
        this.projectSettings = projectSettings;
        Format = new ProjectFormatViewModel(session, projectSettings, ReportStatus);
        Start = new StartViewModel(projectFiles, OpenProjectFromPathAsync, () => OpenProjectAsync(), ReportStatus);
        Export = new ExportViewModel(export, dialogs, session, () => ShowingExport = false, ReportStatus);
        ClipTools = new ClipToolsViewModel(session, timeline, () => SelectedClipId, () => Player.PlayheadFrame, SplitClip, DeleteClip, ReportStatus);
        AddMedia = new AddMediaViewModel(session, AppendAssetToTimeline, AddText, () => ImportMediaCommand.ExecuteAsync(null));
        Inspector = new InspectorViewModel(session, timeline, () => Player.PlayheadFrame, ReportStatus);
        Player.PlayheadMoved += (_, _) => Inspector.Show(SelectedClipId);
        session.Changed += (_, _) => Refresh();
        Refresh();
    }

    public PlayerViewModel Player { get; }
    public InspectorViewModel Inspector { get; }
    public ProjectFormatViewModel Format { get; }
    public StartViewModel Start { get; }
    public ExportViewModel Export { get; }
    public ClipToolsViewModel ClipTools { get; }
    public AddMediaViewModel AddMedia { get; }

    public IReadOnlyList<string> FilterNames { get; } = FilterPresets.All.Select(preset => preset.Name).ToList();

    public ObservableCollection<AssetItemViewModel> Assets { get; } = [];
    public ObservableCollection<TrackItemViewModel> Tracks { get; } = [];
    public ObservableCollection<RulerTickViewModel> RulerTicks { get; } = [];

    [ObservableProperty]
    public partial string Title { get; private set; } = AppName;

    [ObservableProperty]
    public partial string ProjectSummary { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "Crea o abre un proyecto para empezar.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExportScreen), nameof(IsEasyEditing), nameof(IsAdvancedEditing))]
    [NotifyCanExecuteChangedFor(nameof(SaveProjectCommand), nameof(SaveProjectAsCommand), nameof(ImportMediaCommand),
        nameof(AddVideoTrackCommand), nameof(AddAudioTrackCommand), nameof(ShowExportCommand), nameof(AddTextCommand),
        nameof(ToggleAdvancedModeCommand))]
    public partial bool HasProject { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExportScreen), nameof(IsEasyEditing), nameof(IsAdvancedEditing))]
    public partial bool ShowingExport { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEasyEditing), nameof(IsAdvancedEditing), nameof(ModeActionLabel))]
    public partial bool AdvancedMode { get; private set; }

    public bool IsExportScreen => HasProject && ShowingExport;
    public bool IsEasyEditing => HasProject && !ShowingExport && !AdvancedMode;
    public bool IsAdvancedEditing => HasProject && !ShowingExport && AdvancedMode;
    public string ModeActionLabel => AdvancedMode ? "Modo fácil" : "Modo avanzado";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AppendSelectedAssetCommand))]
    public partial AssetItemViewModel? SelectedAsset { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AppendSelectedAssetCommand))]
    public partial TrackItemViewModel? SelectedTrack { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SplitSelectedClipCommand), nameof(DeleteSelectedClipCommand))]
    public partial Guid? SelectedClipId { get; private set; }

    [ObservableProperty]
    public partial double TimelineWidth { get; private set; }

    [ObservableProperty]
    public partial bool HasAssets { get; private set; }

    [ObservableProperty]
    public partial bool HasTracks { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCaptionsCommand))]
    public partial bool IsTranscribing { get; private set; }

    [ObservableProperty]
    public partial bool TimelineTipVisible { get; private set; }

    [ObservableProperty]
    public partial bool MagnetEnabled { get; private set; } = true;

    private CancellationTokenSource? transcription;

    public async Task OpenProjectFromPathAsync(string filePath) =>
        await RunAsync(() => projectFiles.OpenAsync(filePath, CancellationToken.None), "Proyecto abierto.");

    /// <summary>Imports dropped files; when they land on the timeline they are also placed there, one after another.</summary>
    public Task ImportFilesAsync(IReadOnlyList<string> filePaths, Guid? trackId, double? dropPixels)
    {
        EnsureProjectForDrop();
        long? nextFrame = dropPixels is { } pixels
            ? TimelineScale.PixelsToFrames(pixels, session.RequireProject().Settings.FrameRate)
            : null;

        return ImportEachAsync(filePaths, imported =>
        {
            if (nextFrame is not { } frame)
            {
                return;
            }

            Run(() =>
            {
                var clip = timeline.PlaceAsset(imported.Id, trackId, frame);
                var project = session.RequireProject();
                trackId = project.GetTrackOfClip(clip.Id).Id;
                nextFrame = clip.TimelineEndFrame(project.Settings.FrameRate);
                SelectedClipId = clip.Id;
            }, $"Añadido a la línea de tiempo: {imported.DisplayName}");
        });
    }

    /// <summary>Files dropped on the start screen: a new project takes the shape of the first visual medium.</summary>
    public async Task StartWithFilesAsync(IReadOnlyList<string> filePaths)
    {
        if (!Start.CreateProject())
        {
            return;
        }

        var shaped = false;
        await ImportEachAsync(filePaths, imported =>
        {
            if (!shaped && imported.IsVisual && imported.IsFile)
            {
                Run(() => projectSettings.MatchCanvasTo(imported.Id), "Proyecto creado con la forma de tu video.");
                shaped = true;
            }

            AppendAssetToTimeline(imported.Id);
        });
    }

    public Task ImportFilesAndAppendAsync(IReadOnlyList<string> filePaths)
    {
        EnsureProjectForDrop();
        return ImportEachAsync(filePaths, imported => AppendAssetToTimeline(imported.Id));
    }

    public void PlaceAsset(Guid assetId, Guid? trackId, double pixels)
    {
        var frame = TimelineScale.PixelsToFrames(pixels, session.RequireProject().Settings.FrameRate);
        Run(() => SelectedClipId = timeline.PlaceAsset(assetId, trackId, frame).Id, "Clip añadido a la línea de tiempo.");
    }

    public void SelectClip(Guid clipId) => SelectedClipId = clipId;

    public void ShiftClip(Guid clipId, double deltaPixels) =>
        EditByPixels(deltaPixels, frames => timeline.ShiftClip(clipId, frames), "Clip movido.");

    /// <summary>
    /// Moves the clip by the dragged distance, snapping its edges to the start, the playhead and the nearby
    /// borders of other clips so pieces click together instead of leaving tiny gaps.
    /// </summary>
    public void MoveClipWithMagnet(Guid clipId, double deltaPixels)
    {
        var project = session.Project;
        if (project is null || project.TryGetClip(clipId) is not { } clip)
        {
            return;
        }

        var frameRate = project.Settings.FrameRate;
        var duration = clip.TimelineDurationFrames(frameRate);
        var desiredStart = Math.Max(0, clip.TimelineStartFrame + TimelineScale.PixelsToFrames(deltaPixels, frameRate));
        var threshold = Math.Max(1, TimelineScale.PixelsToFrames(MagnetThresholdPixels, frameRate));

        var targets = new List<long> { 0, Player.PlayheadFrame };
        foreach (var other in project.Tracks.SelectMany(track => track.Clips).Where(other => other.Id != clipId))
        {
            targets.Add(other.TimelineStartFrame);
            targets.Add(other.TimelineEndFrame(frameRate));
        }

        var deltaFrames = TimelineMagnet.SnapStart(desiredStart, duration, targets, threshold) - clip.TimelineStartFrame;
        if (deltaFrames != 0)
        {
            Run(() => timeline.ShiftClip(clipId, deltaFrames), "Clip movido.");
        }
    }

    public void TrimClipStart(Guid clipId, double deltaPixels) =>
        EditByPixels(deltaPixels, frames => timeline.TrimClipStart(clipId, frames), "Inicio del clip recortado.");

    public void TrimClipEnd(Guid clipId, double deltaPixels) =>
        EditByPixels(deltaPixels, frames => timeline.TrimClipEnd(clipId, frames), "Final del clip recortado.");

    [RelayCommand]
    private void NewProject() => Run(() => projectFiles.CreateNew(NewProjectName), "Proyecto nuevo creado.");

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var filePath = await dialogs.PickProjectToOpenAsync();
        if (filePath is not null)
        {
            await OpenProjectFromPathAsync(filePath);
        }
    }

    [RelayCommand(CanExecute = nameof(HasProject))]
    private Task SaveProjectAsync() =>
        session.FilePath is null
            ? SaveProjectAsAsync()
            : RunAsync(() => projectFiles.SaveAsync(CancellationToken.None), "Proyecto guardado.");

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task SaveProjectAsAsync()
    {
        var filePath = await dialogs.PickProjectSaveLocationAsync(session.RequireProject().Name);
        if (filePath is not null)
        {
            await RunAsync(() => projectFiles.SaveAsAsync(filePath, CancellationToken.None), "Proyecto guardado.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasProject), IncludeCancelCommand = true)]
    private async Task ImportMediaAsync(CancellationToken cancellationToken)
    {
        var filePaths = await dialogs.PickMediaFilesAsync();
        foreach (var filePath in filePaths)
        {
            await RunAsync(() => mediaImport.ImportAsync(filePath, cancellationToken), $"Importado: {System.IO.Path.GetFileName(filePath)}");
        }
    }

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ShowExport()
    {
        Export.Prepare();
        ShowingExport = true;
    }

    public void CloseExport() => ShowingExport = false;

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ToggleAdvancedMode() => AdvancedMode = !AdvancedMode;

    [RelayCommand]
    private void Help() =>
        StatusMessage = "Arrastra tus videos, toca un clip para editarlo y pulsa «Exportar video» cuando termines.";

    [RelayCommand]
    private void HideTimelineTip()
    {
        TimelineTipVisible = false;
        preferences.DismissTimelineTip();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Run(history.Undo, "Deshecho.");

    private bool CanUndo() => history.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Run(history.Redo, "Rehecho.");

    private bool CanRedo() => history.CanRedo;

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void AddText() =>
        Run(() => SelectedClipId = timeline.AddText(Player.PlayheadFrame).Id, "Texto añadido. Escríbelo en el panel de la derecha.");

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void AddVideoTrack() => Run(() => timeline.AddTrack(TrackKind.Video), "Pista de video añadida.");

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void AddAudioTrack() => Run(() => timeline.AddTrack(TrackKind.Audio), "Pista de audio añadida.");

    [RelayCommand(CanExecute = nameof(CanAppendSelectedAsset))]
    private void AppendSelectedAsset() =>
        Run(() => SelectedClipId = timeline.AppendClip(SelectedTrack!.Id, SelectedAsset!.Id).Id, "Clip añadido a la pista.");

    private bool CanAppendSelectedAsset() => SelectedAsset is not null && SelectedTrack is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedClip))]
    private void SplitSelectedClip() => SplitClip(SelectedClipId!.Value);

    [RelayCommand(CanExecute = nameof(HasSelectedClip))]
    private void DeleteSelectedClip() => DeleteClip(SelectedClipId!.Value);

    public void SplitClip(Guid clipId) =>
        Run(() => timeline.SplitClip(clipId, Player.PlayheadFrame), "Clip dividido en el cabezal.");

    public void DuplicateClip(Guid clipId) =>
        Run(() => SelectedClipId = timeline.DuplicateClip(clipId), "Clip duplicado a continuación.");

    public void DeleteClip(Guid clipId)
    {
        if (SelectedClipId == clipId)
        {
            SelectedClipId = null;
        }

        Run(() => timeline.RemoveClip(clipId), "Clip eliminado.");
    }

    public void ShowClipProperties(Guid clipId)
    {
        SelectClip(clipId);
        StatusMessage = "Edita sus propiedades y efectos en el panel de la derecha.";
    }

    public IReadOnlyList<EffectMenuEntry> AppliedEffects(Guid clipId) =>
        session.Project?.TryGetClip(clipId)?.Effects
            .Select(effect => new EffectMenuEntry(effect.Type, EffectLabels.ForEffect(effect.Type)))
            .ToList() ?? [];

    public void RemoveEffect(Guid clipId, EffectMenuEntry effect) =>
        Run(() => timeline.ResetEffect(clipId, effect.Type), $"Efecto quitado: {effect.Label}.");

    public void ResetSelectedClipEffect(string effectType)
    {
        if (SelectedClipId is { } clipId)
        {
            Run(() => timeline.ResetEffect(clipId, effectType), "Ajuste restablecido.");
        }
    }

    public async Task GenerateCaptionsAsync(Guid clipId)
    {
        if (IsTranscribing)
        {
            StatusMessage = "Ya se están generando subtítulos.";
            return;
        }

        using var run = transcription = new CancellationTokenSource();
        IsTranscribing = true;
        StatusMessage = "Transcribiendo el audio del clip…";
        try
        {
            var count = await captions.GenerateAsync(clipId, run.Token);
            StatusMessage = $"Subtítulos creados: {count} frases en una pista nueva.";
        }
        catch (VineeeeeaException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
            StatusMessage = "Subtítulos cancelados.";
        }
        finally
        {
            transcription = null;
            IsTranscribing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsTranscribing))]
    private void CancelCaptions() => transcription?.Cancel();

    public void ApplyFilter(Guid clipId, string filterName) =>
        Run(() => timeline.ApplyFilter(clipId, filterName), $"Filtro aplicado: {filterName}.");

    public void RemoveColorFilters(Guid clipId) =>
        Run(() => timeline.RemoveColorFilters(clipId), "Filtro quitado.");

    public void RemoveAllEffects(Guid clipId) =>
        Run(() => timeline.RemoveAllEffects(clipId), "Efectos quitados del clip.");

    public void AppendAssetToTimeline(Guid assetId) =>
        Run(() => SelectedClipId = timeline.AppendAssetToTimeline(assetId).Id, "Añadido al final de la línea de tiempo.");

    public void RemoveAsset(Guid assetId) =>
        Run(() => mediaLibrary.RemoveAsset(assetId), "Medio eliminado del proyecto junto con sus clips.");

    public void ToggleTrackMuted(TrackItemViewModel track) =>
        Run(() => timeline.SetTrackMuted(track.Id, !track.IsMuted), track.IsMuted ? "Sonido de la pista activado." : "Pista silenciada.");

    public void ToggleTrackHidden(TrackItemViewModel track) =>
        Run(() => timeline.SetTrackHidden(track.Id, !track.IsHidden), track.IsHidden ? "Pista visible." : "Pista oculta.");

    public void ToggleTrackLocked(TrackItemViewModel track) =>
        Run(() => timeline.SetTrackLocked(track.Id, !track.IsLocked), track.IsLocked ? "Pista desbloqueada." : "Pista bloqueada.");

    [RelayCommand]
    private void ToggleMagnet() => MagnetEnabled = !MagnetEnabled;

    /// <summary>Moves a clip after a drag, honouring the magnet toggle (snap on, free on).</summary>
    public void DropClip(Guid clipId, double deltaPixels)
    {
        if (MagnetEnabled)
        {
            MoveClipWithMagnet(clipId, deltaPixels);
        }
        else
        {
            ShiftClip(clipId, deltaPixels);
        }
    }

    public void RemoveTrack(Guid trackId) => Run(() => timeline.RemoveTrack(trackId), "Pista eliminada.");

    private bool HasSelectedClip() => SelectedClipId is not null;

    partial void OnSelectedClipIdChanged(Guid? value)
    {
        RebuildTracks();
        ClipTools.Refresh();
        Inspector.Show(value);
    }

    private void EnsureProjectForDrop()
    {
        if (session.Project is null)
        {
            Run(() => projectFiles.CreateNew(NewProjectName), "Proyecto nuevo creado.");
        }
    }

    private async Task ImportEachAsync(IReadOnlyList<string> filePaths, Action<MediaAsset> afterImport)
    {
        foreach (var filePath in filePaths)
        {
            MediaAsset? imported = null;
            await RunAsync(
                async () => imported = await mediaImport.ImportAsync(filePath, CancellationToken.None),
                $"Importado: {System.IO.Path.GetFileName(filePath)}");

            if (imported is not null)
            {
                afterImport(imported);
            }
        }
    }

    private void EditByPixels(double deltaPixels, Action<long> edit, string successMessage)
    {
        var deltaFrames = TimelineScale.PixelsToFrames(deltaPixels, session.RequireProject().Settings.FrameRate);
        if (deltaFrames != 0)
        {
            Run(() => edit(deltaFrames), successMessage);
        }
    }

    private void ReportStatus(string message) => StatusMessage = message;

    private void Run(Action action, string successMessage)
    {
        try
        {
            action();
            StatusMessage = successMessage;
        }
        catch (VineeeeeaException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private async Task RunAsync(Func<Task> action, string successMessage)
    {
        try
        {
            await action();
            StatusMessage = successMessage;
        }
        catch (VineeeeeaException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Operación cancelada.";
        }
    }

    private void Refresh()
    {
        var project = session.Project;
        HasProject = project is not null;
        Title = project is null ? AppName : $"{project.Name}{(session.HasUnsavedChanges ? " *" : string.Empty)} — {AppName}";
        ProjectSummary = project is null
            ? string.Empty
            : $"{project.Settings.Width}×{project.Settings.Height} · {project.Settings.FrameRate.ToFramesPerSecond():0.##} fps · {project.Settings.AudioSampleRate} Hz";
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();

        if (SelectedClipId is { } clipId && project?.TryGetClip(clipId) is null)
        {
            SelectedClipId = null;
        }

        var selectedAssetId = SelectedAsset?.Id;
        Assets.Clear();
        foreach (var asset in project?.Assets.Where(asset => asset.IsFile) ?? [])
        {
            Assets.Add(AssetItemViewModel.From(asset));
            RequestPreviews(asset);
        }

        SelectedAsset = Assets.FirstOrDefault(asset => asset.Id == selectedAssetId);
        HasAssets = Assets.Count > 0;
        RebuildTracks();
        Player.Refresh();
        Format.Refresh();
        if (project is null)
        {
            Start.RefreshRecents();
        }

        AddMedia.Refresh();
        ClipTools.Refresh();
        Inspector.Show(SelectedClipId);
    }

    private void RebuildTracks()
    {
        var project = session.Project;
        var selectedTrackId = SelectedTrack?.Id;
        Tracks.Clear();
        RulerTicks.Clear();
        if (project is null)
        {
            TimelineWidth = 0;
            HasTracks = false;
            return;
        }

        var firstVideoTrackId = project.Tracks.FirstOrDefault(track => track.Kind == TrackKind.Video)?.Id;
        foreach (var track in project.Tracks)
        {
            Tracks.Add(ToTrackItem(project, track, track.Id == firstVideoTrackId));
        }

        SelectedTrack = Tracks.FirstOrDefault(track => track.Id == selectedTrackId);
        HasTracks = Tracks.Count > 0;
        TimelineWidth = Tracks.Select(track => track.ContentWidth).DefaultIfEmpty(TimelineScale.TrailingSpacePixels).Max();
        var seconds = (int)(TimelineWidth / TimelineScale.PixelsPerSecond);
        for (var second = 0; second <= seconds; second += RulerLabelEverySeconds)
        {
            RulerTicks.Add(new RulerTickViewModel(
                second * TimelineScale.PixelsPerSecond,
                TimeSpan.FromSeconds(second).ToString(@"m\:ss", System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private TrackItemViewModel ToTrackItem(Project project, Track track, bool isFirstVideoTrack)
    {
        var frameRate = project.Settings.FrameRate;
        var clips = track.Clips
            .Select(clip =>
            {
                var asset = project.GetAsset(clip.AssetId);
                return new ClipItemViewModel(
                    clip.Id,
                    asset.DisplayName,
                    TimelineScale.FramesToPixels(clip.TimelineStartFrame, frameRate),
                    TimelineScale.FramesToPixels(clip.TimelineDurationFrames(frameRate), frameRate),
                    clip.Id == SelectedClipId,
                    thumbnails.GetValueOrDefault(asset.Id),
                    WaveformFor(asset, clip));
            })
            .ToList();
        var appendX = TimelineScale.FramesToPixels(project.TrackEndFrame(track), frameRate);
        var contentWidth = appendX + TimelineScale.TrailingSpacePixels;
        var status = string.Join(" · ", new[]
        {
            track.Kind switch
            {
                TrackKind.Video => "Video",
                TrackKind.Text => "Texto",
                _ => "Audio",
            },
            track.IsMuted ? "silenciada" : null,
            track.IsHidden ? "oculta" : null,
            track.IsLocked ? "bloqueada" : null,
        }.OfType<string>());
        var easyName = track.Kind switch
        {
            TrackKind.Video => isFirstVideoTrack ? "Tu video" : "Encima",
            TrackKind.Audio => "Música",
            _ => "Texto",
        };
        return new TrackItemViewModel(track.Id, track.Kind, track.Name, easyName, status, track.IsVisual, track.IsMuted, track.IsHidden, track.IsLocked, clips, contentWidth, appendX);
    }

    private IImage? WaveformFor(MediaAsset asset, Clip clip)
    {
        if (!waveforms.TryGetValue(asset.Id, out var waveform))
        {
            return null;
        }

        var pixelsPerUs = (double)waveform.PixelSize.Width / asset.DurationUs;
        var left = (int)Math.Clamp(clip.SourceInUs * pixelsPerUs, 0, waveform.PixelSize.Width - 1);
        var width = (int)Math.Clamp(clip.SourceDurationUs * pixelsPerUs, 1, waveform.PixelSize.Width - left);
        return new CroppedBitmap(waveform, new PixelRect(left, 0, width, waveform.PixelSize.Height));
    }

    private void RequestPreviews(MediaAsset asset)
    {
        if (requestedPreviews.Add(asset.Id))
        {
            _ = LoadPreviewsAsync(asset);
        }
    }

    private async Task LoadPreviewsAsync(MediaAsset asset)
    {
        try
        {
            if (await clipPreviews.GetThumbnailAsync(asset, CancellationToken.None) is { } thumbnailPath)
            {
                thumbnails[asset.Id] = new Bitmap(thumbnailPath);
            }

            if (await clipPreviews.GetWaveformAsync(asset, CancellationToken.None) is { } waveformPath)
            {
                waveforms[asset.Id] = new Bitmap(waveformPath);
            }

            RebuildTracks();
        }
        catch (VineeeeeaException exception)
        {
            requestedPreviews.Remove(asset.Id);
            StatusMessage = exception.Message;
        }
    }
}
