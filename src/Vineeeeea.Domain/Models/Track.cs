using System.Collections.Immutable;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Domain.Models;

public sealed record Track
{
    public Guid Id { get; }
    public TrackKind Kind { get; }
    public string Name { get; }
    public bool IsMuted { get; private init; }
    public bool IsHidden { get; private init; }
    public bool IsLocked { get; private init; }
    public ImmutableList<Clip> Clips { get; private init; } = [];

    public Track(Guid id, TrackKind kind, string name, bool isMuted, bool isHidden, bool isLocked = false)
    {
        Id = NameRules.RequireId(id, "La pista");
        Kind = kind;
        Name = NameRules.Normalize(name, "El nombre de la pista");
        IsMuted = isMuted;
        IsHidden = isHidden;
        IsLocked = isLocked;
    }

    public bool Accepts(MediaKind mediaKind) => (Kind, mediaKind) switch
    {
        (TrackKind.Video, MediaKind.Video or MediaKind.Image) => true,
        (TrackKind.Audio, MediaKind.Audio) => true,
        (TrackKind.Text, MediaKind.Text) => true,
        _ => false,
    };

    public bool IsVisual => Kind is TrackKind.Video or TrackKind.Text;

    internal Track WithMuted(bool isMuted) => this with { IsMuted = isMuted };

    internal Track WithHidden(bool isHidden) => this with { IsHidden = isHidden };

    internal Track WithLocked(bool isLocked) => this with { IsLocked = isLocked };

    internal Track WithClips(ImmutableList<Clip> clips) =>
        this with { Clips = clips.Sort((left, right) => left.TimelineStartFrame.CompareTo(right.TimelineStartFrame)) };
}
