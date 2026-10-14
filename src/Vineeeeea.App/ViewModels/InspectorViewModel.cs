using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Vineeeeea.App.Utils;
using Vineeeeea.Application.Controllers;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.App.ViewModels;

public sealed partial class InspectorViewModel(
    EditorSession session,
    TimelineController timeline,
    Func<long> playheadFrame,
    Action<string> reportStatus) : ObservableObject
{
    private const double MaxSpeedOnSlider = 10;

    private Guid? shownClipId;
    private bool showingModel;

    public ObservableCollection<EffectParameterViewModel> Parameters { get; } = [];

    [ObservableProperty]
    public partial string Title { get; private set; } = "Selecciona un clip para editarlo.";

    [ObservableProperty]
    public partial bool HasClip { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedText))]
    public partial double Speed { get; set; } = DomainLimits.NormalSpeed;

    public string SpeedText => string.Concat(Speed.ToString("0.00", CultureInfo.CurrentCulture), "×");

    public double MinSpeed => DomainLimits.MinSpeed;

    public double MaxSpeed => MaxSpeedOnSlider;

    public IReadOnlyList<TextColorPreset> TextColorPresets => TextColors.All;
    public double MinTextSize => DomainLimits.MinTextSize;
    public double MaxTextSize => DomainLimits.MaxTextSize;

    [ObservableProperty]
    public partial bool IsTextClip { get; private set; }

    [ObservableProperty]
    public partial string TextContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double TextSize { get; set; } = DomainLimits.DefaultTextSize;

    [ObservableProperty]
    public partial TextColorPreset? TextColor { get; set; }

    [ObservableProperty]
    public partial bool TextHasBackground { get; set; }

    [ObservableProperty]
    public partial bool TextIsBold { get; set; }

    public void Show(Guid? clipId)
    {
        var project = session.Project;
        var clip = clipId is { } id ? project?.TryGetClip(id) : null;
        if (project is null || clip is null)
        {
            shownClipId = null;
            HasClip = false;
            Title = "Selecciona un clip para editarlo.";
            Parameters.Clear();
            return;
        }

        var asset = project.GetAsset(clip.AssetId);
        var frameRate = project.Settings.FrameRate;
        var frameOnClip = Math.Clamp(playheadFrame(), clip.TimelineStartFrame, clip.TimelineEndFrame(frameRate) - 1);
        var sourceUs = clip.SourceUsAt(frameOnClip, frameRate);
        var rows = EffectCatalog.All
            .Where(spec => spec.AppliesTo(asset))
            .SelectMany(spec => spec.Parameters.Select(parameter => (Spec: spec, Parameter: parameter)))
            .ToList();

        if (shownClipId != clip.Id || Parameters.Count != rows.Count)
        {
            Parameters.Clear();
            foreach (var (spec, parameter) in rows)
            {
                Parameters.Add(new EffectParameterViewModel(
                    spec.Type,
                    parameter.Name,
                    parameter == spec.Parameters[0] ? EffectLabels.ForEffect(spec.Type) : null,
                    EffectLabels.For(spec.Type, parameter.Name),
                    parameter.Min,
                    parameter.Max,
                    parameter.Keyframable,
                    OnValueEdited,
                    OnAddKeyframe,
                    OnClearKeyframes));
            }
        }

        shownClipId = clip.Id;
        HasClip = true;
        showingModel = true;
        Speed = clip.Speed;
        IsTextClip = asset.Text is not null;
        if (asset.Text is { } text)
        {
            TextContent = text.Content;
            TextSize = text.Size;
            TextColor = TextColorPresets.FirstOrDefault(preset => preset.Hex == text.Color);
            TextHasBackground = text.HasBackground;
            TextIsBold = text.IsBold;
        }

        showingModel = false;
        Title = asset.DisplayName;
        foreach (var (row, (spec, parameter)) in Parameters.Zip(rows))
        {
            var effect = clip.FindEffect(spec.Type);
            row.ShowModelValue(
                effect?.ValueAt(parameter.Name, sourceUs) ?? parameter.Default,
                effect?.Keyframes.ContainsKey(parameter.Name) ?? false);
        }
    }

    partial void OnSpeedChanged(double value)
    {
        if (!showingModel && shownClipId is { } clipId)
        {
            Run(() => timeline.SetClipSpeed(clipId, Math.Round(value, 2)));
        }
    }

    partial void OnTextContentChanged(string value) => SaveText();

    partial void OnTextSizeChanged(double value) => SaveText();

    partial void OnTextColorChanged(TextColorPreset? value) => SaveText();

    partial void OnTextHasBackgroundChanged(bool value) => SaveText();

    partial void OnTextIsBoldChanged(bool value) => SaveText();

    private void SaveText()
    {
        if (showingModel || !IsTextClip || shownClipId is not { } clipId)
        {
            return;
        }

        var color = TextColor?.Hex ?? DomainLimits.DefaultTextColor;
        Run(() => timeline.UpdateText(clipId, new TextStyle(TextContent, TextSize, color, TextHasBackground, TextIsBold)));
    }

    private void OnValueEdited(EffectParameterViewModel row, double value) =>
        Run(() => timeline.SetEffectParameter(shownClipId!.Value, row.EffectType, row.Parameter, value, playheadFrame()));

    private void OnAddKeyframe(EffectParameterViewModel row) =>
        Run(() => timeline.AddKeyframe(shownClipId!.Value, row.EffectType, row.Parameter, playheadFrame()));

    private void OnClearKeyframes(EffectParameterViewModel row) =>
        Run(() => timeline.ClearKeyframes(shownClipId!.Value, row.EffectType, row.Parameter));

    private void Run(Action edit)
    {
        try
        {
            edit();
        }
        catch (VineeeeeaException exception)
        {
            reportStatus(exception.Message);
        }
    }
}
