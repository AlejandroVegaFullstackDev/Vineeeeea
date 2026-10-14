using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Vineeeeea.App.ViewModels;

/// <summary>Fills the keyframe diamond with ink when the parameter is animated, and leaves it hollow otherwise.</summary>
public sealed class KeyframeFillConverter : IValueConverter
{
    public static readonly KeyframeFillConverter Instance = new();

    private static readonly IBrush Filled = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Filled : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
