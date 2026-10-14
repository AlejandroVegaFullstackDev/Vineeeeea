using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.App.ViewModels;

public sealed record StyleOption(string Title, string? FilterName);

public sealed partial class SpeedOptionViewModel(double value, string label) : ObservableObject
{
    public double Value { get; } = value;
    public string Label { get; } = label;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// The "¿Qué le hacemos a este clip?" panel of the easy editor: the six action tiles plus the Style and Light
/// controls. Every change goes through the timeline commands, so it is all undoable.
/// </summary>
public sealed partial class ClipToolsViewModel : ObservableObject
{
    private const double LightScale = 100;

    private readonly EditorSession session;
    private readonly TimelineController timeline;
    private readonly Func<Guid?> selectedClipId;
    private readonly Func<long> playheadFrame;
    private readonly Action<Guid> splitAtPlayhead;
    private readonly Action<Guid> removeClip;
    private readonly Action<string> reportStatus;
    private bool showingModel;

    public ClipToolsViewModel(
        EditorSession session,
        TimelineController timeline,
        Func<Guid?> selectedClipId,
        Func<long> playheadFrame,
        Action<Guid> splitAtPlayhead,
        Action<Guid> removeClip,
        Action<string> reportStatus)
    {
        this.session = session;
        this.timeline = timeline;
        this.selectedClipId = selectedClipId;
        this.playheadFrame = playheadFrame;
        this.splitAtPlayhead = splitAtPlayhead;
        this.removeClip = removeClip;
        this.reportStatus = reportStatus;
    }

    public IReadOnlyList<SpeedOptionViewModel> Speeds { get; } =
        [new(0.5, "0.5×"), new(1, "1×"), new(1.5, "1.5×"), new(2, "2×")];

    public IReadOnlyList<StyleOption> Styles { get; } =
    [
        new("Original", null),
        new("Vivo", "Vívido"),
        new("Cálido", "Cálido"),
        new("B y N", "Blanco y negro"),
    ];

    [ObservableProperty]
    public partial bool HasClip { get; private set; }

    [ObservableProperty]
    public partial string ClipName { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVisualClip { get; private set; }

    [ObservableProperty]
    public partial bool HasAudio { get; private set; }

    [ObservableProperty]
    public partial bool GreenScreenOn { get; private set; }

    [ObservableProperty]
    public partial bool CleanAudioOn { get; private set; }

    [ObservableProperty]
    public partial double Light { get; set; }

    [ObservableProperty]
    public partial double Volume { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsMuted { get; private set; }

    public void Refresh()
    {
        var project = session.Project;
        var clip = selectedClipId() is { } id ? project?.TryGetClip(id) : null;
        if (project is null || clip is null)
        {
            HasClip = false;
            ClipName = string.Empty;
            return;
        }

        var asset = project.GetAsset(clip.AssetId);
        showingModel = true;
        HasClip = true;
        ClipName = asset.DisplayName;
        IsVisualClip = asset.IsVisual;
        HasAudio = asset.HasAudio;
        foreach (var option in Speeds)
        {
            option.IsSelected = Math.Abs(option.Value - clip.Speed) < 0.001;
        }

        Light = Math.Round((clip.FindEffect(EffectCatalog.Brightness)?.Parameters[EffectCatalog.Amount] ?? 0) * LightScale);
        var gain = clip.FindEffect(EffectCatalog.Volume)?.Parameters[EffectCatalog.Gain] ?? 1;
        IsMuted = gain == 0;
        Volume = IsMuted ? 1 : gain;
        GreenScreenOn = clip.FindEffect(EffectCatalog.ChromaKey) is not null;
        CleanAudioOn = clip.FindEffect(EffectCatalog.Denoise) is not null || clip.FindEffect(EffectCatalog.Normalize) is not null;
        showingModel = false;
    }

    [RelayCommand]
    private void CutHere()
    {
        if (selectedClipId() is { } clipId)
        {
            splitAtPlayhead(clipId);
        }
    }

    [RelayCommand]
    private void Trim() =>
        reportStatus("Arrastra los bordes del clip en la línea de tiempo para acortarlo.");

    [RelayCommand]
    private void Rotate()
    {
        if (selectedClipId() is { } clipId)
        {
            Run(() => timeline.RotateClipQuarterTurn(clipId), "Clip girado 90°.");
        }
    }

    [RelayCommand]
    private void FlipHorizontal()
    {
        if (selectedClipId() is { } clipId)
        {
            var current = session.Project?.TryGetClip(clipId)?.FindEffect(EffectCatalog.Flip)?.Parameters[EffectCatalog.Horizontal] ?? 0;
            Run(() => timeline.SetEffectParameter(clipId, EffectCatalog.Flip, EffectCatalog.Horizontal, current >= 0.5 ? 0 : 1, playheadFrame()),
                "Clip volteado.");
        }
    }

    [RelayCommand]
    private void ToggleGreenScreen()
    {
        if (selectedClipId() is not { } clipId)
        {
            return;
        }

        if (GreenScreenOn)
        {
            Run(() => timeline.ResetEffect(clipId, EffectCatalog.ChromaKey), "Pantalla verde quitada.");
        }
        else
        {
            Run(() => timeline.SetEffectParameter(clipId, EffectCatalog.ChromaKey, EffectCatalog.Similarity,
                EffectInstance.CreateDefault(EffectCatalog.ChromaKey).Parameters[EffectCatalog.Similarity], playheadFrame()),
                "Pantalla verde activada: pon este clip sobre otro para ver el fondo.");
        }
    }

    [RelayCommand]
    private void ToggleCleanAudio()
    {
        if (selectedClipId() is not { } clipId)
        {
            return;
        }

        if (CleanAudioOn)
        {
            Run(
                () =>
                {
                    timeline.ResetEffect(clipId, EffectCatalog.Denoise);
                    timeline.ResetEffect(clipId, EffectCatalog.Normalize);
                },
                "Audio en su estado original.");
        }
        else
        {
            Run(
                () =>
                {
                    timeline.SetEffectParameter(clipId, EffectCatalog.Denoise, EffectCatalog.Amount, 0.6, playheadFrame());
                    timeline.SetEffectParameter(clipId, EffectCatalog.Normalize, EffectCatalog.Target, EffectCatalog.DefaultLoudnessLufs, playheadFrame());
                },
                "Voz limpia: quité ruido y nivelé el volumen.");
        }
    }

    [RelayCommand]
    private void Remove()
    {
        if (selectedClipId() is { } clipId)
        {
            removeClip(clipId);
        }
    }

    [RelayCommand]
    private void SetSpeed(SpeedOptionViewModel option)
    {
        if (selectedClipId() is { } clipId)
        {
            Run(() => timeline.SetClipSpeed(clipId, option.Value), $"Velocidad: {option.Label}.");
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        if (selectedClipId() is { } clipId)
        {
            Run(() => timeline.SetEffectParameter(clipId, EffectCatalog.Volume, EffectCatalog.Gain, IsMuted ? 1 : 0, playheadFrame()),
                IsMuted ? "Sonido activado." : "Clip silenciado.");
        }
    }

    [RelayCommand]
    private void ApplyStyle(StyleOption style)
    {
        if (selectedClipId() is not { } clipId)
        {
            return;
        }

        Run(
            () =>
            {
                if (style.FilterName is null)
                {
                    timeline.RemoveColorFilters(clipId);
                }
                else
                {
                    timeline.ApplyFilter(clipId, style.FilterName);
                }
            },
            style.FilterName is null ? "Estilo original." : $"Estilo: {style.Title}.");
    }

    partial void OnLightChanged(double value)
    {
        if (!showingModel && selectedClipId() is { } clipId)
        {
            Run(() => timeline.SetEffectParameter(clipId, EffectCatalog.Brightness, EffectCatalog.Amount, value / LightScale, playheadFrame()), null);
        }
    }

    partial void OnVolumeChanged(double value)
    {
        if (!showingModel && !IsMuted && selectedClipId() is { } clipId)
        {
            Run(() => timeline.SetEffectParameter(clipId, EffectCatalog.Volume, EffectCatalog.Gain, value, playheadFrame()), null);
        }
    }

    private void Run(Action edit, string? successMessage)
    {
        try
        {
            edit();
            if (successMessage is not null)
            {
                reportStatus(successMessage);
            }
        }
        catch (VineeeeeaException exception)
        {
            reportStatus(exception.Message);
        }
    }
}
