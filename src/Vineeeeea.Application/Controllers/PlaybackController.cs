using System.Diagnostics;
using Vineeeeea.Application.Config;
using Vineeeeea.Application.Dtos;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.Application.Controllers;

public sealed class PlaybackController(EditorSession session, IRenderEngine renderEngine, IAudioPlayer audioPlayer, AppConfig config)
{
    private const int MinPreviewSide = 2;

    public async Task<VideoFrame> RenderStillAsync(long timelineFrame, CancellationToken cancellationToken)
    {
        var project = session.RequireProject();
        var size = PreviewSizeFor(project.Settings);
        if (timelineFrame < 0 || timelineFrame >= project.EndFrame())
        {
            return VideoFrame.Black(timelineFrame, size);
        }

        var still = VideoFrame.Black(timelineFrame, size);
        await renderEngine.StreamVideoAsync(
            RenderPlanBuilder.Build(project, timelineFrame, timelineFrame + 1, size),
            (pixels, _) =>
            {
                still = new VideoFrame(timelineFrame, size, pixels);
                return ValueTask.CompletedTask;
            },
            cancellationToken);
        return still;
    }

    /// <summary>
    /// Plays video and audio in real time from <paramref name="fromFrame"/>, or from the start when it is already at the end.
    /// Late video frames are dropped instead of slowing the clock, so picture and sound stay together.
    /// </summary>
    public async Task PlayAsync(long fromFrame, Action<VideoFrame> onFrame, CancellationToken cancellationToken)
    {
        var project = session.RequireProject();
        var endFrame = project.EndFrame();
        if (endFrame == 0)
        {
            return;
        }

        var startFrame = fromFrame >= endFrame - 1 ? 0 : Math.Max(0, fromFrame);
        var size = PreviewSizeFor(project.Settings);
        var plan = RenderPlanBuilder.Build(project, startFrame, endFrame, size);
        var frameDuration = TimeSpan.FromSeconds(1 / project.Settings.FrameRate.ToFramesPerSecond());

        using var audioStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var audio = plan.HasAudio ? audioPlayer.PlayAsync(plan, audioStop.Token) : Task.CompletedTask;
        var clock = Stopwatch.StartNew();
        var frame = startFrame;

        try
        {
            await renderEngine.StreamVideoAsync(
                plan,
                async (pixels, token) =>
                {
                    var wait = frameDuration * (frame - startFrame) - clock.Elapsed;
                    if (wait > TimeSpan.Zero)
                    {
                        await Task.Delay(wait, token);
                    }

                    if (-wait < frameDuration)
                    {
                        onFrame(new VideoFrame(frame, size, pixels));
                    }

                    frame++;
                },
                cancellationToken);
        }
        finally
        {
            await audioStop.CancelAsync();
        }

        try
        {
            await audio;
        }
        catch (OperationCanceledException) when (audioStop.IsCancellationRequested)
        {
        }
    }

    public FrameSize PreviewSizeFor(ProjectSettings settings)
    {
        var scale = Math.Min(1.0, (double)config.PreviewMaxSide / Math.Max(settings.Width, settings.Height));
        return new FrameSize(EvenSide(settings.Width * scale), EvenSide(settings.Height * scale));
    }

    private static int EvenSide(double side) => Math.Max(MinPreviewSide, (int)Math.Round(side / 2) * 2);
}
