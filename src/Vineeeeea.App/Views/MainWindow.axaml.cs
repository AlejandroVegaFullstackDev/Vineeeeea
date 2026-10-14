using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Vineeeeea.App.Utils;
using Vineeeeea.App.ViewModels;

namespace Vineeeeea.App.Views;

public partial class MainWindow : Window
{
    private const double ClickTolerancePixels = 3;
    private const string TrimStartTag = "start";

    private Point? dragOrigin;
    private (PointerPressedEventArgs Press, Guid AssetId)? pendingAssetDrag;

    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        MediaList.AddHandler(PointerPressedEvent, OnMediaListPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        MediaList.AddHandler(PointerMovedEvent, OnMediaListPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private enum DropZone
    {
        Timeline,
        Viewer,
        Elsewhere,
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>
    /// Listens on the list itself while tunnelling: each list row captures the pointer when pressed,
    /// so a handler on the row's content would never see the movement that starts the drag.
    /// </summary>
    private void OnMediaListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        pendingAssetDrag = e.GetCurrentPoint(MediaList).Properties.IsLeftButtonPressed && DataContextUnder<AssetItemViewModel>(e.Source) is { } asset
            ? (e, asset.Id)
            : null;
    }

    private async void OnMediaListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (pendingAssetDrag is not ({ } press, var assetId))
        {
            return;
        }

        if (!e.GetCurrentPoint(MediaList).Properties.IsLeftButtonPressed)
        {
            pendingAssetDrag = null;
            return;
        }

        var moved = e.GetPosition(MediaList) - press.GetPosition(MediaList);
        if (Math.Abs(moved.X) + Math.Abs(moved.Y) < ClickTolerancePixels)
        {
            return;
        }

        pendingAssetDrag = null;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(DragFormats.LibraryAsset, assetId.ToString()));
        await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Copy);
    }

    private void OnClipContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: ClipItemViewModel clip } || ViewModel is not { } viewModel)
        {
            return;
        }

        e.Handled = true;
        viewModel.SelectClip(clip.Id);
        var effects = viewModel.AppliedEffects(clip.Id);
        var effectItems = effects
            .Select(effect => (Control)MenuItemFor($"Quitar {effect.Label}", () => viewModel.RemoveEffect(clip.Id, effect)))
            .ToList();
        if (effectItems.Count == 0)
        {
            effectItems.Add(new MenuItem { Header = "Sin efectos aplicados", IsEnabled = false });
        }
        else
        {
            effectItems.Add(new Separator());
            effectItems.Add(MenuItemFor("Quitar todos los efectos", () => viewModel.RemoveAllEffects(clip.Id)));
        }

        var filterItems = viewModel.FilterNames
            .Select(name => (Control)MenuItemFor(name, () => viewModel.ApplyFilter(clip.Id, name)))
            .Append(new Separator())
            .Append(MenuItemFor("Quitar filtro", () => viewModel.RemoveColorFilters(clip.Id)))
            .ToList();

        ShowMenu(
            MenuItemFor("Dividir en el cabezal", () => viewModel.SplitClip(clip.Id), "Ctrl+B"),
            MenuItemFor("Duplicar", () => viewModel.DuplicateClip(clip.Id)),
            MenuItemFor("Eliminar", () => viewModel.DeleteClip(clip.Id), "Delete"),
            new Separator(),
            new MenuItem { Header = "Filtros", ItemsSource = filterItems },
            MenuItemFor("Subtítulos automáticos", () => _ = viewModel.GenerateCaptionsAsync(clip.Id)),
            new MenuItem { Header = "Efectos", ItemsSource = effectItems },
            new Separator(),
            MenuItemFor("Propiedades…", () => viewModel.ShowClipProperties(clip.Id)));
    }

    private void OnTrackContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: TrackItemViewModel track } || ViewModel is not { } viewModel)
        {
            return;
        }

        e.Handled = true;
        var items = new List<Control>
        {
            MenuItemFor(track.IsMuted ? "Activar sonido" : "Silenciar", () => viewModel.ToggleTrackMuted(track)),
        };
        if (track.IsVisual)
        {
            items.Add(MenuItemFor(track.IsHidden ? "Mostrar" : "Ocultar", () => viewModel.ToggleTrackHidden(track)));
        }

        items.Add(new Separator());
        items.Add(MenuItemFor("Eliminar pista", () => viewModel.RemoveTrack(track.Id)));
        ShowMenu([.. items]);
    }

    private void OnAssetContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: AssetItemViewModel asset } || ViewModel is not { } viewModel)
        {
            return;
        }

        e.Handled = true;
        ShowMenu(
            MenuItemFor("Añadir al final de la línea de tiempo", () => viewModel.AppendAssetToTimeline(asset.Id)),
            new Separator(),
            MenuItemFor("Eliminar del proyecto", () => viewModel.RemoveAsset(asset.Id)));
    }

    private void ShowMenu(params Control[] items) => new ContextMenu { ItemsSource = items }.Open(this);

    private static MenuItem MenuItemFor(string header, Action action, string? gesture = null)
    {
        var item = new MenuItem { Header = header, InputGesture = gesture is null ? null : KeyGesture.Parse(gesture) };
        item.Click += (_, _) => action();
        return item;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var accepted = e.DataTransfer.Contains(DataFormat.File)
            || (e.DataTransfer.Contains(DragFormats.LibraryAsset) && ZoneOf(e) != DropZone.Elsewhere);
        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var zone = ZoneOf(e);
        var trackId = DataContextUnder<TrackItemViewModel>(e.Source)?.Id;
        var timelinePixels = Math.Max(0, e.GetPosition(Ruler).X);

        if (e.DataTransfer.TryGetValue(DragFormats.LibraryAsset) is { } assetText && Guid.TryParse(assetText, out var assetId))
        {
            if (zone == DropZone.Timeline)
            {
                viewModel.PlaceAsset(assetId, trackId, timelinePixels);
            }
            else if (zone == DropZone.Viewer)
            {
                viewModel.AppendAssetToTimeline(assetId);
            }

            return;
        }

        if (e.DataTransfer.TryGetFiles() is { } files)
        {
            var paths = files.Select(file => file.TryGetLocalPath()).OfType<string>().ToList();
            if (!viewModel.HasProject)
            {
                await viewModel.StartWithFilesAsync(paths);
                return;
            }

            await (zone switch
            {
                DropZone.Timeline => viewModel.ImportFilesAsync(paths, trackId, timelinePixels),
                DropZone.Viewer => viewModel.ImportFilesAndAppendAsync(paths),
                _ => viewModel.ImportFilesAsync(paths, trackId: null, dropPixels: null),
            });
        }
    }

    private DropZone ZoneOf(DragEventArgs e) =>
        IsOver(e, TimelinePanel) ? DropZone.Timeline
        : IsOver(e, Viewer) ? DropZone.Viewer
        : DropZone.Elsewhere;

    private static bool IsOver(DragEventArgs e, Control control) => new Rect(control.Bounds.Size).Contains(e.GetPosition(control));

    private static T? DataContextUnder<T>(object? source)
        where T : class =>
        (source as Visual)?.GetSelfAndVisualAncestors()
            .OfType<StyledElement>()
            .Select(element => element.DataContext)
            .OfType<T>()
            .FirstOrDefault();

    private void OnTrackMuteClick(object? sender, RoutedEventArgs e) => WithTrack(sender, track => ViewModel?.ToggleTrackMuted(track));

    private void OnTrackHideClick(object? sender, RoutedEventArgs e) => WithTrack(sender, track => ViewModel?.ToggleTrackHidden(track));

    private void OnTrackLockClick(object? sender, RoutedEventArgs e) => WithTrack(sender, track => ViewModel?.ToggleTrackLocked(track));

    private static void WithTrack(object? sender, Action<TrackItemViewModel> action)
    {
        if (sender is Control { DataContext: TrackItemViewModel track })
        {
            action(track);
        }
    }

    private void OnResetEffectClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string effectType })
        {
            ViewModel?.ResetSelectedClipEffect(effectType);
        }
    }

    private void OnRulerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Visual ruler)
        {
            ViewModel?.Player.SeekToPixels(e.GetPosition(ruler).X);
        }
    }

    private void OnClipPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border clip && e.GetCurrentPoint(clip).Properties.IsLeftButtonPressed)
        {
            BeginDrag(clip, e);
        }
    }

    private void OnClipPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DragDelta(e) is { } delta && sender is Border { RenderTransform: TranslateTransform offset })
        {
            offset.X = delta;
        }
    }

    private void OnClipPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (EndDrag(sender, e) is not ({ } delta, { } clip) || ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.SelectClip(clip.Id);
        if (Math.Abs(delta) >= ClickTolerancePixels)
        {
            viewModel.DropClip(clip.Id, delta);
        }
    }

    private void OnTrimPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border handle && e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
        {
            BeginDrag(handle, e);
            e.Handled = true;
        }
    }

    private void OnTrimPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DragDelta(e) is { } delta && sender is Border { RenderTransform: TranslateTransform offset })
        {
            offset.X = delta;
            e.Handled = true;
        }
    }

    private void OnTrimPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (EndDrag(sender, e) is not ({ } delta, { } clip) || ViewModel is not { } viewModel || sender is not Border handle)
        {
            return;
        }

        e.Handled = true;
        viewModel.SelectClip(clip.Id);
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
