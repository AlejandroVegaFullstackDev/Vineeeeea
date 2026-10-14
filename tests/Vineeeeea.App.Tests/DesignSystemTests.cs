using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vineeeeea.App.Views;

namespace Vineeeeea.App.Tests;

/// <summary>
/// Guards the design system added in phase 1: the bundled fonts resolve under their tokens, the
/// class-based base styles apply, and the whole thing renders for design review.
/// </summary>
public class DesignSystemTests
{
    private const string FontsAsset = "avares://Vineeeeea.App/Assets/Fonts";

    [AvaloniaTheory]
    [InlineData("Bricolage Grotesque", FontWeight.Normal)]
    [InlineData("Bricolage Grotesque", FontWeight.Medium)]
    [InlineData("Bricolage Grotesque", FontWeight.SemiBold)]
    [InlineData("Bricolage Grotesque", FontWeight.Bold)]
    [InlineData("JetBrains Mono", FontWeight.Medium)]
    [InlineData("JetBrains Mono", FontWeight.Bold)]
    [InlineData("Bowlby One", FontWeight.Normal)]
    [InlineData("UnifrakturMaguntia", FontWeight.Normal)]
    public void Bundled_fonts_resolve_for_their_weights(string familyName, FontWeight weight)
    {
        var typeface = new Typeface(new FontFamily($"{FontsAsset}#{familyName}"), FontStyle.Normal, weight);

        Assert.True(
            FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface),
            $"{familyName} {weight} should resolve from the bundled fonts.");
        Assert.NotNull(glyphTypeface);
    }

    [AvaloniaFact]
    public void Standard_button_uses_the_design_tokens()
    {
        var button = Show(new Button { Content = "Estándar", Classes = { "standard" } });

        Assert.Equal(42d, button.Height);
        Assert.Equal(new CornerRadius(12), button.CornerRadius);
        Assert.Equal(2.5, button.BorderThickness.Left);

        var presenter = ContentPresenterOf(button);
        Assert.Equal(1, presenter.BoxShadow.Count);
        Assert.Equal(3d, presenter.BoxShadow[0].OffsetX);
        Assert.Equal(3d, presenter.BoxShadow[0].OffsetY);
    }

    [AvaloniaFact]
    public void Primary_button_uses_the_accent_fill()
    {
        var button = Show(new Button { Content = "Listo", Classes = { "primary" } });

        var background = Assert.IsType<SolidColorBrush>(button.Background);
        Assert.Equal(Color.Parse("#D8E50F"), background.Color);
    }

    [AvaloniaFact]
    public void Panel_uses_the_design_tokens()
    {
        var panel = Show(new Border { Classes = { "panel" } });

        Assert.Equal(new CornerRadius(18), panel.CornerRadius);
        Assert.Equal(2.5, panel.BorderThickness.Left);
        Assert.Equal(1, panel.BoxShadow.Count);
        Assert.Equal(4d, panel.BoxShadow[0].OffsetX);
    }

    [AvaloniaFact]
    public void Checked_option_uses_the_selection_fill()
    {
        var toggle = Show(new ToggleButton { Content = "16:9", Classes = { "option" }, IsChecked = true });

        var presenter = ContentPresenterOf(toggle);
        var background = Assert.IsType<SolidColorBrush>(presenter.Background);
        Assert.Equal(Color.Parse("#CDB3E3"), background.Color);
    }

    [AvaloniaFact]
    public void Keyboard_focus_shows_the_focus_ring()
    {
        var button = Show(new Button { Content = "Enfocado", Classes = { "standard" } });

        button.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        var ring = button.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_FocusRing");
        Assert.True(ring.IsVisible);
    }

    [AvaloniaFact]
    public void Every_icon_geometry_loads_and_draws_inside_its_box()
    {
        var icons = Avalonia.Application.Current!.Resources.MergedDictionaries
            .OfType<Avalonia.Controls.ResourceDictionary>()
            .SelectMany(dictionary => dictionary.Keys.OfType<string>())
            .Where(key => key.StartsWith("Icon.", StringComparison.Ordinal) && key != "Icon.StrokeWidth")
            .ToList();

        Assert.True(icons.Count >= 38, $"Se esperaban los iconos del diseño y hay {icons.Count}.");
        foreach (var key in icons)
        {
            var path = Show(new Avalonia.Controls.Shapes.Path { Data = Resource<Geometry>(key), Classes = { "icon" } });
            Assert.Equal(24d, path.Bounds.Width);
            Assert.InRange(path.Data!.Bounds.Right, 0, 24);
            Assert.InRange(path.Data.Bounds.Bottom, 0, 24);
        }
    }

    [AvaloniaFact]
    public void Main_window_uses_the_brand_icon()
    {
        var window = new MainWindow();

        Assert.NotNull(window.Icon);
    }

    [AvaloniaFact]
    public void Disabled_button_dims()
    {
        var button = Show(new Button { Content = "No disponible", Classes = { "standard" }, IsEnabled = false });

        Assert.False(button.IsEnabled);
        Assert.Equal(0.45, button.Opacity);
    }

    [AvaloniaFact]
    [Trait("Category", "Screenshots")]
    public void Captures_the_design_system()
    {
        var outputDirectory = Environment.GetEnvironmentVariable("VINEEEEEA_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        Directory.CreateDirectory(outputDirectory);
        var window = new Window
        {
            Background = Resource<IBrush>("Brush.Bg.App"),
            Width = 640,
            Height = 620,
            Content = BuildGallery(),
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(outputDirectory, "10-sistema-de-diseno.png"), PngBitmapEncoderOptions.Default);
    }

    private static Control BuildGallery()
    {
        var buttons = new StackPanel { Orientation = Orientation.Vertical, Spacing = 20 };
        buttons.Children.Add(new Button { Content = "Estándar", Classes = { "standard" } });
        buttons.Children.Add(new Button { Content = "Primario", Classes = { "primary" } });
        buttons.Children.Add(new Button { Content = "Deshabilitado", Classes = { "standard" }, IsEnabled = false });
        buttons.Children.Add(new ToggleButton { Content = "Opción", Classes = { "option" } });
        buttons.Children.Add(new ToggleButton { Content = "Elegida", Classes = { "option" }, IsChecked = true });

        var panel = new Border
        {
            Classes = { "panel" },
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = buttons,
        };

        var samples = new StackPanel { Spacing = 12 };
        samples.Children.Add(new TextBlock
        {
            Text = "Vineeeeea",
            FontFamily = Resource<FontFamily>("Font.Brand"),
            FontSize = 36,
        });
        samples.Children.Add(new TextBlock
        {
            Text = "Bricolage Grotesque",
            FontFamily = Resource<FontFamily>("Font.UI"),
            FontSize = 18,
        });
        samples.Children.Add(new TextBlock
        {
            Text = "JetBrains Mono 500",
            FontFamily = Resource<FontFamily>("Font.Mono"),
            FontSize = 13,
        });
        samples.Children.Add(panel);

        return new Border { Padding = new Thickness(32), Child = samples };
    }

    private static T Show<T>(T control)
        where T : Control
    {
        var window = new Window { Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return control;
    }

    private static ContentPresenter ContentPresenterOf(Control control)
    {
        return control.GetVisualDescendants().OfType<ContentPresenter>().Single();
    }

    private static T Resource<T>(string key)
    {
        Assert.True(Avalonia.Application.Current!.TryFindResource(key, out var value), $"Missing resource '{key}'.");
        return Assert.IsAssignableFrom<T>(value);
    }
}
