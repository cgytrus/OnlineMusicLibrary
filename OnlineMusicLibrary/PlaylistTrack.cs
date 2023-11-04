using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace OnlineMusicLibrary;

[PrimaryKey(nameof(position), nameof(playlistId)), Index(nameof(playlistId))]
public class PlaylistTrack {
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required uint position { get; init; }
    [ForeignKey(nameof(playlist))]
    public uint playlistId { get; init; }
    [ForeignKey(nameof(track))]
    public uint trackId { get; init; }

    public required Playlist playlist { get; init; }
    public required Track track { get; init; }
}
