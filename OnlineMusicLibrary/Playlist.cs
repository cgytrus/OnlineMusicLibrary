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
    public required string tracks { get; set; }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class CreateDto {
        public required string title { get; init; }
        public uint[]? tracks { get; init; }

        public Playlist ToPlaylist(string username) => new() {
            username = username,
            title = title,
            tracks = tracks is null ? "" : string.Join(',', tracks.Select(x => x.ToString()))
        };
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class UpdateDto {
        public string? title { get; init; }
        public uint[]? tracks { get; init; }

        public void MergeInto(Playlist playlist) {
            if (title is not null)
                playlist.title = title;
            if (tracks is not null)
                playlist.tracks = string.Join(',', tracks.Select(x => x.ToString()));
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
            List<Track.GetDto> tracks = new();
            foreach (string s in playlist.tracks.Split(',')) {
                if (!uint.TryParse(s, out uint id))
                    continue;
                Track? track = db.tracks.Find(id);
                if (track is null)
                    continue;
                tracks.Add(new Track.GetDto(track));
            }
            this.tracks = tracks;
        }
    }
}
