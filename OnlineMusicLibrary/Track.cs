﻿using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Text;

using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;

using SixLabors.ImageSharp.Formats.Jpeg;

using TagLib;

using File = System.IO.File;

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
    public required uint year { get; set; }
    public required string genre { get; set; }
    public required uint trackNumber { get; set; }
    public required uint trackCount { get; set; }
    public required uint discNumber { get; set; }
    public required uint discCount { get; set; }
    public required string lyrics { get; set; }
    public required string listen { get; set; }
    public required string download { get; set; }

    public string artPath => GetArtPath(albumHash);
    public bool HasArt() => File.Exists(artPath);
    private static string GetAlbumHash(string album, string albumArtist) => Convert
        .ToHexString(Crc32.Hash(Encoding.Unicode.GetBytes($"{album}{albumArtist}")))
        .ToLowerInvariant();
    private static string GetArtPath(string albumHash) => Path.Combine(
        Environment.GetEnvironmentVariable("OML_DB_PATH") ??
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnlineMusicLibrary",
        $"{albumHash}.jpg");

    private const int MaxArtSize = 1200;

    private Task SaveArt(string base64) => SaveArt(Convert.FromBase64String(base64));
    private async Task SaveArt(byte[] bytes) {
        string? artDir = Path.GetDirectoryName(artPath);
        if (artDir is not null)
            Directory.CreateDirectory(artDir);

        using Image image = Image.Load(bytes);
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
        public uint? year { get; init; }
        public string? genre { get; init; }
        public uint? trackNumber { get; set; }
        public uint? trackCount { get; set; }
        public uint? discNumber { get; set; }
        public uint? discCount { get; set; }
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
                year = year ?? 0,
                genre = genre ?? "",
                trackNumber = trackNumber ?? 1,
                trackCount = trackCount ?? 1,
                discNumber = discNumber ?? 1,
                discCount = discCount ?? 1,
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
    public class CreateFileDto {
        public required string fileName { get; init; }
        public required string file { get; init; }
        public string? lyrics { get; init; }
        public required string listen { get; init; }
        public string? download { get; init; }

        private class Base64ReadFile : TagLib.File.IFileAbstraction {
            public string Name { get; }
            public Stream ReadStream { get; }
            public Stream? WriteStream => null;
            public void CloseStream(Stream? stream) => stream?.Close();

            public Base64ReadFile(string name, string base64) {
                Name = name;
                ReadStream = new MemoryStream(Convert.FromBase64String(base64), false);
            }
        }

        public async Task<Track> ToTrack(string username) {
            TagLib.File? file = TagLib.File.Create(new Base64ReadFile(fileName, this.file));
            if (file is null)
                throw new InvalidDataException("file is null");

            string album = file.Tag.Album ?? file.Tag.Title ?? "<unknown>";
            string albumArtist = file.Tag.AlbumArtists is null || file.Tag.AlbumArtists.Length == 0 ?
                file.Tag.Performers is null || file.Tag.Performers.Length == 0 ? "<unknown>" :
                    file.Tag.Performers[0] :
                string.Join(" & ", file.Tag.AlbumArtists);

            Track track = new() {
                username = username,
                title = file.Tag.Title ?? "<unknown>",
                artist = file.Tag.Performers is null || file.Tag.Performers.Length == 0 ? "<unknown>" :
                    string.Join(" & ", file.Tag.Performers),
                album = album,
                albumArtist = albumArtist,
                albumHash = GetAlbumHash(album, albumArtist),
                year = file.Tag.Year,
                genre = file.Tag.JoinedGenres ?? "",
                trackNumber = file.Tag.Track == 0 ? 1 : file.Tag.Track,
                trackCount = file.Tag.TrackCount == 0 ? 1 : file.Tag.TrackCount,
                discNumber = file.Tag.Disc == 0 ? 1 : file.Tag.Disc,
                discCount = file.Tag.DiscCount == 0 ? 1 : file.Tag.DiscCount,
                lyrics = lyrics ?? file.Tag.Lyrics ?? "",
                listen = listen,
                download = download ?? listen
            };
            IPicture? art = file.Tag.Pictures?.FirstOrDefault(p => p.Type == PictureType.FrontCover) ??
                file.Tag.Pictures?.FirstOrDefault();
            if (!track.HasArt() && art?.Data?.Data is not null)
                await track.SaveArt(art.Data.Data);
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
        public uint? year { get; init; }
        public string? genre { get; init; }
        public uint? trackNumber { get; set; }
        public uint? trackCount { get; set; }
        public uint? discNumber { get; set; }
        public uint? discCount { get; set; }
        public string? lyrics { get; init; }
        public string? listen { get; init; }
        public string? download { get; init; }

        // ReSharper disable once CognitiveComplexity
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
            if (year.HasValue)
                track.year = year.Value;
            if (genre is not null)
                track.genre = genre;
            if (trackNumber.HasValue)
                track.trackNumber = trackNumber.Value;
            if (trackCount.HasValue)
                track.trackCount = trackCount.Value;
            if (discNumber.HasValue)
                track.discNumber = discNumber.Value;
            if (discCount.HasValue)
                track.discCount = discCount.Value;
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
        public uint id { get; }
        public string title { get; }
        public string artist { get; }
        public string album { get; }
        public string albumArtist { get; }
        public bool hasArt { get; }
        public uint year { get; }
        public string genre { get; }
        public uint trackNumber { get; }
        public uint trackCount { get; }
        public uint discNumber { get; }
        public uint discCount { get; }
        public bool hasLyrics { get; }
        public string listen { get; }
        public string download { get; }

        public GetDto(Track track) {
            id = track.id;
            title = track.title;
            artist = track.artist;
            album = track.album;
            albumArtist = track.albumArtist;
            hasArt = track.HasArt();
            year = track.year;
            genre = track.genre;
            trackNumber = track.trackNumber;
            trackCount = track.trackCount;
            discNumber = track.discNumber;
            discCount = track.discCount;
            hasLyrics = !string.IsNullOrWhiteSpace(track.lyrics);
            listen = track.listen;
            download = track.download;
        }
    }
}
