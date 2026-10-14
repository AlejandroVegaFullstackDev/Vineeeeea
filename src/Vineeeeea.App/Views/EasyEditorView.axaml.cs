using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Vineeeeea.App.ViewModels;

namespace Vineeeeea.App.Views;

public partial class EasyEditorView : UserControl
{
    private const double ClickTolerancePixels = 3;
    private const string TrimStartTag = "start";

    private bool draggingPlayhead;
    private Point? dragOrigin;

    public EasyEditorView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnRulerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Visual ruler)
        {
            ViewModel?.Player.SeekToPixels(e.GetPosition(ruler).X);
        }
    }

    private void OnPlayheadPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control handle && e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
        {
            draggingPlayhead = true;
            e.Pointer.Capture(handle);
            e.Handled = true;
        }
    }

    private void OnPlayheadMoved(object? sender, PointerEventArgs e)
    {
        if (draggingPlayhead)
        {
            ViewModel?.Player.SeekToPixels(e.GetPosition(PlayheadLayer).X);
            e.Handled = true;
        }
    }

    private void OnPlayheadReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (draggingPlayhead)
        {
            draggingPlayhead = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnClipPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Tag: Guid clipId } clip && e.GetCurrentPoint(clip).Properties.IsLeftButtonPressed)
        {
            ViewModel?.SelectClip(clipId);
            BeginDrag(clip, e);
        }
    }

    private void OnClipMoved(object? sender, PointerEventArgs e)
    {
        if (DragDelta(e) is { } delta && sender is Border { RenderTransform: TranslateTransform offset })
        {
            offset.X = delta;
        }
    }

    private void OnClipReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (EndDrag(sender, e) is not ({ } delta, { } clip) || ViewModel is not { } viewModel)
        {
            return;
        }

        if (Math.Abs(delta) >= ClickTolerancePixels)
        {
            viewModel.DropClip(clip.Id, delta);
        }
    }

    private void OnTrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border handle && e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
        {
            BeginDrag(handle, e);
            e.Handled = true;
        }
    }

    private void OnTrimMoved(object? sender, PointerEventArgs e)
    {
        if (DragDelta(e) is { } delta && sender is Border { RenderTransform: TranslateTransform offset })
        {
            offset.X = delta;
            e.Handled = true;
        }
    }

    private void OnTrimReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (EndDrag(sender, e) is not ({ } delta, { } clip) || ViewModel is not { } viewModel || sender is not Border handle)
        {
            return;
        }

        e.Handled = true;
        if (Equals(handle.Tag, TrimStartTag))
        {
            viewModel.TrimClipStart(clip.Id, delta);
        }
        else
        {
            viewModel.TrimClipEnd(clip.Id, delta);
        }
    }

    private void BeginDrag(Border border, PointerPressedEventArgs e)
    {
        dragOrigin = e.GetPosition(this);
        border.RenderTransform = new TranslateTransform();
        e.Pointer.Capture(border);
    }

    private double? DragDelta(PointerEventArgs e) => dragOrigin is { } origin ? e.GetPosition(this).X - origin.X : null;

    private (double Delta, ClipItemViewModel Clip)? EndDrag(object? sender, PointerReleasedEventArgs e)
    {
        if (DragDelta(e) is not { } delta || sender is not Border { DataContext: ClipItemViewModel clip } border)
        {
            return null;
        }

        dragOrigin = null;
        border.RenderTransform = null;
        e.Pointer.Capture(null);
        return (delta, clip);
    }
}
