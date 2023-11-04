using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;

namespace OnlineMusicLibrary;

[Index(nameof(username))]
public class Playlist {
    [Key]
    public uint id { get; init; }
    public required string username { get; init; }
    public required string title { get; set; }

    // ReSharper disable once SuggestBaseTypeForParameter
    private async Task AddTracks(ApplicationDbContext db, uint[] tracks) {
        uint skipped = 0;
        for (uint i = 0; i < tracks.Length; i++) {
            Track? track = await db.tracks.FindAsync(tracks[i]);
            if (track is null) {
                skipped++;
                continue;
            }
            PlaylistTrack playlistTrack = new() {
                position = i - skipped,
                playlist = this,
                track = track
            };
            db.playlistTracks.Add(playlistTrack);
        }
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class CreateDto {
        public required string title { get; init; }
        public uint[]? tracks { get; init; }

        public async Task<Playlist> ToPlaylist(ApplicationDbContext db, string username) {
            Playlist playlist = new() {
                username = username,
                title = title
            };
            if (tracks is not null)
                await playlist.AddTracks(db, tracks);
            return playlist;
        }
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class UpdateDto {
        public string? title { get; init; }
        public uint[]? tracks { get; init; }

        public async Task MergeInto(ApplicationDbContext db, Playlist playlist) {
            if (title is not null)
                playlist.title = title;
            if (tracks is null)
                return;
            db.playlistTracks.RemoveRange(db.playlistTracks.Where(pt => pt.playlist == playlist));
            await playlist.AddTracks(db, tracks);
        }
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class GetDto {
        public required uint id { get; init; }
        public required string username { get; init; }
        public required string title { get; init; }
        public required IReadOnlyList<Track.GetDto> tracks { get; init; }

        public static async Task<GetDto> From(ApplicationDbContext db, Playlist playlist) => new() {
            id = playlist.id,
            username = playlist.username,
            title = playlist.title,
            tracks = await db.playlistTracks
                .Where(pt => pt.playlist == playlist)
                .OrderBy(pt => pt.position)
                .Select(pt => new Track.GetDto(pt.track))
                .ToListAsync()
        };
    }
}
