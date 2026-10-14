using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vineeeeea.App.Services;
using Vineeeeea.App.Utils;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;

namespace Vineeeeea.App.ViewModels;

public sealed partial class PlayerViewModel(EditorSession session, PlaybackController playback, Action<string> reportStatus) : ObservableObject
{
    private readonly PreviewBitmapWriter previewWriter = new();
    private CancellationTokenSource? stillRequest;
    private CancellationTokenSource? playbackRun;
    private VideoFrame? pendingPlaybackFrame;

    public event EventHandler? PlayheadMoved;

    [ObservableProperty]
    public partial Bitmap? Preview { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayheadX), nameof(PlayheadBallX))]
    public partial double PlayheadPosition { get; set; }

    [ObservableProperty]
    public partial double TimelineEndFrame { get; private set; }

    [ObservableProperty]
    public partial string PlayheadTime { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentTimeLabel { get; private set; } = "0:00";

    [ObservableProperty]
    public partial string TotalTimeLabel { get; private set; } = "0:00";

    [ObservableProperty]
    public partial string Timecode { get; private set; } = "00:00:00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackLabel))]
    public partial bool IsPlaying { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TogglePlaybackCommand), nameof(GoToStartCommand), nameof(GoToEndCommand),
        nameof(StepBackCommand), nameof(StepForwardCommand))]
    public partial bool HasProject { get; private set; }

    public string PlaybackLabel => IsPlaying ? "Pausa" : "Reproducir";

    public long PlayheadFrame => (long)Math.Round(PlayheadPosition);

    public double PlayheadX => session.Project is { } project
        ? TimelineScale.FramesToPixels(PlayheadFrame, project.Settings.FrameRate)
        : 0;

    /// <summary>Left edge of the 20 px grab handle so it is centred on the playhead line.</summary>
    public double PlayheadBallX => PlayheadX - 10;

    public void Refresh()
    {
        var project = session.Project;
        HasProject = project is not null;
        if (project is null)
        {
            Preview = null;
            PlayheadPosition = 0;
            TimelineEndFrame = 0;
            PlayheadTime = string.Empty;
            CurrentTimeLabel = TotalTimeLabel = "0:00";
            return;
        }

        TimelineEndFrame = Math.Max(0, project.EndFrame() - 1);
        PlayheadPosition = Math.Min(PlayheadPosition, TimelineEndFrame);
        PlayheadTime = FormatPlayhead();
        UpdateEasyLabels();
        OnPropertyChanged(nameof(PlayheadX));
        OnPropertyChanged(nameof(PlayheadBallX));
        if (!IsPlaying)
        {
            _ = RenderStillAsync();
        }
    }

    public void SeekToPixels(double pixels)
    {
        if (session.Project is { } project)
        {
            PlayheadPosition = Math.Clamp(TimelineScale.PixelsToFrames(pixels, project.Settings.FrameRate), 0, TimelineEndFrame);
        }
    }

    [RelayCommand(CanExecute = nameof(HasProject), AllowConcurrentExecutions = true)]
    private async Task TogglePlaybackAsync()
    {
        if (playbackRun is not null)
        {
            await playbackRun.CancelAsync();
            return;
        }

        using var run = playbackRun = new CancellationTokenSource();
        stillRequest?.Cancel();
        IsPlaying = true;
        var fromFrame = PlayheadFrame;
        try
        {
            await Task.Run(() => playback.PlayAsync(fromFrame, OnPlaybackFrame, run.Token), run.Token);
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
        }
        catch (VineeeeeaException exception)
        {
            reportStatus(exception.Message);
        }
        finally
        {
            playbackRun = null;
            IsPlaying = false;
        }
    }

    partial void OnPlayheadPositionChanged(double value)
    {
        PlayheadTime = FormatPlayhead();
        UpdateEasyLabels();
        PlayheadMoved?.Invoke(this, EventArgs.Empty);
        if (!IsPlaying)
        {
            _ = RenderStillAsync();
        }
    }

    private void UpdateEasyLabels()
    {
        if (session.Project is not { } project)
        {
            return;
        }

        var frameRate = project.Settings.FrameRate;
        CurrentTimeLabel = TimeFormatting.FormatSeconds(frameRate.FramesToMicroseconds(PlayheadFrame));
        TotalTimeLabel = TimeFormatting.FormatSeconds(frameRate.FramesToMicroseconds(project.EndFrame()));
        Timecode = TimeFormatting.FormatTimecode(PlayheadFrame, frameRate);
    }

    public void StepFrames(long delta) => PlayheadPosition = Math.Clamp(PlayheadFrame + delta, 0, TimelineEndFrame);

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void GoToStart() => PlayheadPosition = 0;

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void GoToEnd() => PlayheadPosition = TimelineEndFrame;

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void StepBack() => StepFrames(-1);

    [RelayCommand(CanExecute = nameof(HasProject))]
    private void StepForward() => StepFrames(1);

    private void OnPlaybackFrame(VideoFrame frame)
    {
        if (Interlocked.Exchange(ref pendingPlaybackFrame, frame) is null)
        {
            Dispatcher.UIThread.Post(PresentPendingPlaybackFrame);
        }
    }

    private void PresentPendingPlaybackFrame()
    {
        var frame = Interlocked.Exchange(ref pendingPlaybackFrame, null);
        if (frame is not null && IsPlaying)
        {
            Preview = previewWriter.Write(frame);
            PlayheadPosition = frame.TimelineFrame;
        }
    }

    private async Task RenderStillAsync()
    {
        if (session.Project is null)
        {
            return;
        }

        stillRequest?.Cancel();
        using var request = stillRequest = new CancellationTokenSource();
        try
        {
            var frame = await playback.RenderStillAsync(PlayheadFrame, request.Token);
            if (!request.IsCancellationRequested)
            {
                Preview = previewWriter.Write(frame);
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        catch (VineeeeeaException exception)
        {
            reportStatus(exception.Message);
        }
        finally
        {
            if (stillRequest == request)
            {
                stillRequest = null;
            }
        }
    }

    private string FormatPlayhead()
    {
        if (session.Project is not { } project)
        {
            return string.Empty;
        }

        var frameRate = project.Settings.FrameRate;
        return $"{TimeFormatting.FormatMicroseconds(frameRate.FramesToMicroseconds(PlayheadFrame))} / " +
               TimeFormatting.FormatMicroseconds(frameRate.FramesToMicroseconds(project.EndFrame()));
    }
}
