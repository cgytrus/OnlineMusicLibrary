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
            if (tracks is null)
                return playlist;
            uint skipped = 0;
            for (uint i = 0; i < tracks.Length; i++) {
                Track? track = await db.tracks.FindAsync(tracks[i]);
                if (track is null) {
                    skipped++;
                    continue;
                }
                PlaylistTrack playlistTrack = new() {
                    position = i - skipped,
                    playlist = playlist,
                    track = track
                };
                db.playlistTracks.Add(playlistTrack);
            }
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
            uint skipped = 0;
            for (uint i = 0; i < tracks.Length; i++) {
                Track? track = await db.tracks.FindAsync(tracks[i]);
                if (track is null) {
                    skipped++;
                    continue;
                }
                PlaylistTrack playlistTrack = new() {
                    position = i - skipped,
                    playlist = playlist,
                    track = track
                };
                db.playlistTracks.Add(playlistTrack);
            }
        }
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class GetDto {
        public uint id { get; }
        public string username { get; }
        public string title { get; }
        public IReadOnlyList<Track.GetDto> tracks { get; }
        public GetDto(ApplicationDbContext db, Playlist playlist) {
            id = playlist.id;
            username = playlist.username;
            title = playlist.title;
            tracks = db.playlistTracks
                .Where(tp => tp.playlist == playlist)
                .OrderBy(t => t.position)
                .Select(t => new Track.GetDto(t.track))
                .ToList();
        }
    }
}
