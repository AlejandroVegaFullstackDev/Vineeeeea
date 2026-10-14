using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vineeeeea.App.ViewModels;

public sealed partial class EffectParameterViewModel(
    string effectType,
    string parameter,
    string? groupTitle,
    string label,
    double minimum,
    double maximum,
    bool keyframable,
    Action<EffectParameterViewModel, double> onValueEdited,
    Action<EffectParameterViewModel> onAddKeyframe,
    Action<EffectParameterViewModel> onClearKeyframes) : ObservableObject
{
    private bool showingModelValue;

    public string EffectType { get; } = effectType;
    public string Parameter { get; } = parameter;
    public string? GroupTitle { get; } = groupTitle;
    public bool StartsGroup => GroupTitle is not null;
    public string Label { get; } = label;
    public double Minimum { get; } = minimum;
    public double Maximum { get; } = maximum;
    public bool Keyframable { get; } = keyframable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    public partial double Value { get; set; }

    [ObservableProperty]
    public partial bool HasKeyframes { get; private set; }

    public string ValueText => Value.ToString("0.00", CultureInfo.CurrentCulture);

    public void ShowModelValue(double value, bool hasKeyframes)
    {
        showingModelValue = true;
        Value = value;
        HasKeyframes = hasKeyframes;
        showingModelValue = false;
    }

    partial void OnValueChanged(double value)
    {
        if (!showingModelValue)
        {
            onValueEdited(this, value);
        }
    }

    [RelayCommand]
    private void AddKeyframe() => onAddKeyframe(this);

    [RelayCommand]
    private void ClearKeyframes() => onClearKeyframes(this);
}
