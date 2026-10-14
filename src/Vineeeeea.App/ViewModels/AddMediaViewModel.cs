using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vineeeeea.App.Utils;
using Vineeeeea.Application.Services;
using Vineeeeea.Domain.Models;

namespace Vineeeeea.App.ViewModels;

public enum MediaCategory
{
    VideosAndPhotos,
    MusicAndSound,
    Text,
}

public sealed record EasyMediaItem(Guid Id, string Name, string Line, bool IsAudio);

/// <summary>The left "Agregar" column of the easy editor: three plain-language categories and the media inside each.</summary>
public sealed partial class AddMediaViewModel : ObservableObject
{
    private readonly EditorSession session;
    private readonly Action<Guid> appendToTimeline;
    private readonly Action addText;
    private readonly Func<System.Threading.Tasks.Task> importFromComputer;

    public AddMediaViewModel(
        EditorSession session,
        Action<Guid> appendToTimeline,
        Action addText,
        Func<System.Threading.Tasks.Task> importFromComputer)
    {
        this.session = session;
        this.appendToTimeline = appendToTimeline;
        this.addText = addText;
        this.importFromComputer = importFromComputer;
    }

    public ObservableCollection<EasyMediaItem> Media { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideosAndPhotos), nameof(IsMusicAndSound), nameof(IsText), nameof(ShowsMediaList))]
    public partial MediaCategory Category { get; set; } = MediaCategory.VideosAndPhotos;

    [ObservableProperty]
    public partial bool HasMedia { get; private set; }

    public bool IsVideosAndPhotos => Category == MediaCategory.VideosAndPhotos;
    public bool IsMusicAndSound => Category == MediaCategory.MusicAndSound;
    public bool IsText => Category == MediaCategory.Text;
    public bool ShowsMediaList => Category != MediaCategory.Text;

    public void Refresh()
    {
        Media.Clear();
        var project = session.Project;
        if (project is not null && Category != MediaCategory.Text)
        {
            var placed = project.Tracks.SelectMany(track => track.Clips).Select(clip => clip.AssetId).ToHashSet();
            foreach (var asset in project.Assets.Where(asset => asset.IsFile && BelongsToCategory(asset)))
            {
                var duration = TimeFormatting.FormatMicroseconds(asset.DurationUs);
                var usage = placed.Contains(asset.Id) ? "ya está en tu video" : "toca + para usarlo";
                Media.Add(new EasyMediaItem(asset.Id, asset.DisplayName, $"{duration} · {usage}", asset.Kind == MediaKind.Audio));
            }
        }

        HasMedia = Media.Count > 0;
    }

    [RelayCommand]
    private void ChooseVideosAndPhotos() => Category = MediaCategory.VideosAndPhotos;

    [RelayCommand]
    private void ChooseMusicAndSound() => Category = MediaCategory.MusicAndSound;

    [RelayCommand]
    private void ChooseText() => Category = MediaCategory.Text;

    [RelayCommand]
    private void Add(EasyMediaItem item) => appendToTimeline(item.Id);

    [RelayCommand]
    private void AddText() => addText();

    [RelayCommand]
    private System.Threading.Tasks.Task SearchOnComputer() => importFromComputer();

    partial void OnCategoryChanged(MediaCategory value) => Refresh();

    private bool BelongsToCategory(MediaAsset asset) => Category switch
    {
        MediaCategory.VideosAndPhotos => asset.Kind is MediaKind.Video or MediaKind.Image,
        MediaCategory.MusicAndSound => asset.Kind == MediaKind.Audio,
        _ => false,
    };
}
