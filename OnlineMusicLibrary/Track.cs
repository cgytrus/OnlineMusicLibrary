using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Text;

using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace OnlineMusicLibrary;

[Index(nameof(username))]
public class Track {
    [Key]
    public uint id { get; init; }
    public required string username { get; init; }
    public required string title { get; set; }
    public required string artist { get; set; }
    public required string album { get; set; }
    public required string albumArtist { get; set; }
    public required string albumHash { get; set; }
    public required string lyrics { get; set; }
    public required string listen { get; set; }
    public required string download { get; set; }

    public string artPath => GetArtPath(albumHash);
    public bool HasArt() => File.Exists(artPath);

    public static string GetAlbumHash(string album, string albumArtist) => Convert
        .ToHexString(Crc32.Hash(Encoding.Unicode.GetBytes($"{album}{albumArtist}")))
        .ToLowerInvariant();

    private const int MaxArtSize = 1200;

    private static string GetArtPath(string albumHash) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnlineMusicLibrary",
        $"{albumHash}.jpg");

    private async Task SaveArt(string base64) {
        string? artDir = Path.GetDirectoryName(artPath);
        if (artDir is not null)
            Directory.CreateDirectory(artDir);

        using Image image = Image.Load(Convert.FromBase64String(base64));
        // resize and crop the image
        image.Mutate(ctx => {
            int width = ctx.GetCurrentSize().Width;
            int height = ctx.GetCurrentSize().Height;
            double aspectRatio = (double)width / height;
            if (width > MaxArtSize) {
                width = MaxArtSize;
                height = (int)Math.Round(width / aspectRatio);
            }
            if (height > MaxArtSize) {
                height = MaxArtSize;
                width = (int)Math.Round(height * aspectRatio);
            }
            if (ctx.GetCurrentSize().Width != width || ctx.GetCurrentSize().Height != height)
                ctx.Resize(width, height);
            int targetCrop = Math.Min(width, height);
            ctx.Crop(new Rectangle((width - targetCrop) / 2, (height - targetCrop) / 2, targetCrop, targetCrop));
        });
        await image.SaveAsJpegAsync(artPath, new JpegEncoder {
            Quality = 85
        });
    }

    private void CopyArt(string fromAlbumHash) {
        string prevPath = GetArtPath(fromAlbumHash);
        if (!File.Exists(prevPath))
            return;
        File.Copy(prevPath, artPath);
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class CreateDto {
        public required string title { get; init; }
        public required string artist { get; init; }
        public string? album { get; init; }
        public string? albumArtist { get; init; }
        public required string art { get; init; }
        public string? lyrics { get; init; }
        public required string listen { get; init; }
        public string? download { get; init; }

        public async Task<Track> ToTrack(string username) {
            Track track = new() {
                username = username,
                title = title,
                artist = artist,
                album = album ?? title,
                albumArtist = albumArtist ?? artist,
                albumHash = GetAlbumHash(album ?? title, albumArtist ?? artist),
                lyrics = lyrics ?? "",
                listen = listen,
                download = download ?? listen
            };
            if (!track.HasArt())
                await track.SaveArt(art);
            return track;
        }
    }

    [UsedImplicitly]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class UpdateDto {
        public string? title { get; init; }
        public string? artist { get; init; }
        public string? album { get; init; }
        public string? albumArtist { get; init; }
        public string? art { get; init; }
        public string? lyrics { get; init; }
        public string? listen { get; init; }
        public string? download { get; init; }

        public async Task MergeInto(Track track) {
            if (title is not null)
                track.title = title;
            if (artist is not null)
                track.artist = artist;
            if (album is not null)
                track.album = album;
            if (albumArtist is not null)
                track.albumArtist = albumArtist;
            if (art is not null)
                await track.SaveArt(art);
            else if (album is not null || albumArtist is not null) {
                string prevAlbumHash = track.albumHash;
                track.albumHash = GetAlbumHash(track.album, track.albumArtist);
                track.CopyArt(prevAlbumHash);
            }
            if (lyrics is not null)
                track.lyrics = lyrics;
            if (listen is not null)
                track.listen = listen;
            if (download is not null)
                track.download = download;
        }
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class GetDto {
        public uint id { get; init; }
        public string title { get; set; }
        public string artist { get; set; }
        public string album { get; set; }
        public string albumArtist { get; set; }
        public string listen { get; set; }

        public GetDto(Track track) {
            id = track.id;
            title = track.title;
            artist = track.artist;
            album = track.album;
            albumArtist = track.albumArtist;
            listen = track.listen;
        }
    }
}
