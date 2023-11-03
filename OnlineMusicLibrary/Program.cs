using Microsoft.EntityFrameworkCore;

using OnlineMusicLibrary;

using TagLib;

using YoutubeDLSharp;

using File = System.IO.File;

//await YoutubeDLSharp.Utils.DownloadBinaries();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin();
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
    });
});

WebApplication app = builder.Build();

app.UseCors();

RouteGroupBuilder userGroup = app.MapGroup("/user");
userGroup.MapGet("/verify", async (HttpContext ctx, ApplicationDbContext db) =>
    await TryAuthorize(ctx, db) is null ? Results.Text("Invalid token") : Results.Text(""));
userGroup.MapGet("/{username}/tracks", (ApplicationDbContext db, string username) => {
    return Results.Ok(db.tracks
        .Where(x => x.username == username)
        .AsEnumerable()
        .Select(x => new Track.GetDto(x))
        .ToArray());
});
userGroup.MapGet("/{username}/playlists", async (ApplicationDbContext db, string username) => {
    return Results.Ok(await db.playlists
        .Where(x => x.username == username)
        .Select(x => x.id)
        .ToArrayAsync());
});

RouteGroupBuilder trackGroup = app.MapGroup("/track");
trackGroup.MapPost("/", async (HttpContext ctx, ApplicationDbContext db, Track.CreateDto input) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    Track track = await input.ToTrack(user.username);
    db.tracks.Add(track);
    await db.SaveChangesAsync();
    return Results.Created($"/track/{track.id}", new Track.GetDto(track));
});
trackGroup.MapPut("/{id}", async (HttpContext ctx, ApplicationDbContext db, uint id, Track.UpdateDto input) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    if (await db.tracks.FindAsync(id) is not { } track)
        return Results.NotFound();

    if (track.username != user.username)
        return Results.Unauthorized();

    string prevAlbumHash = track.albumHash;
    string prevArtPart = track.artPath;

    await input.MergeInto(track);
    await db.SaveChangesAsync();

    if (File.Exists(prevArtPart) && !db.tracks.Any(x => x.albumHash == prevAlbumHash))
        File.Delete(prevArtPart);

    return Results.NoContent();
});
trackGroup.MapGet("/{id}", async (ApplicationDbContext db, uint id) =>
    await db.tracks.FindAsync(id) is { } track ?
        Results.Ok(new Track.GetDto(track)) :
        Results.NotFound());
trackGroup.MapGet("/{id}/art", async (ApplicationDbContext db, uint id) =>
    await db.tracks.FindAsync(id) is { } track ?
        track.HasArt() ?
            Results.File(track.artPath, "media/jpeg", Path.GetFileName(track.artPath)) :
            Results.NotFound() :
        Results.NotFound());
trackGroup.MapGet("/{id}/lyrics", async (ApplicationDbContext db, uint id) =>
    await db.tracks.FindAsync(id) is { } track ?
        track.lyrics is { } lyrics ?
            Results.Text(lyrics, "text/plain") :
            Results.NotFound() :
        Results.NotFound());
trackGroup.MapGet("/{id}/download", async (HttpContext ctx, ApplicationDbContext db, uint id) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    if (await db.tracks.FindAsync(id) is not { } track)
        return Results.NotFound();

    YoutubeDL ytdl = new(1);

    RunResult<string> res = await ytdl.RunAudioDownload(track.download);
    if (!res.Success)
        return Results.Problem(string.Join('\n', res.ErrorOutput), null, 500, "Error downloading audio");
    string path = res.Data;

    TagLib.File? file;
    try { file = TagLib.File.Create(path); }
    catch (Exception ex) {
        if (File.Exists(path))
            File.Delete(path);
        return Results.Problem(ex.ToString(), null, 500, "Error reading metadata");
    }
    if (file is null) {
        if (File.Exists(path))
            File.Delete(path);
        return Results.Problem("file is null", null, 500, "Error reading metadata");
    }

    try {
        file.Tag.Title = track.title;
        file.Tag.Performers = new[] { track.artist };
        file.Tag.Album = track.album;
        file.Tag.AlbumArtists = new[] { track.albumArtist };
        file.Tag.Year = track.year;
        if (!string.IsNullOrWhiteSpace(track.genre))
            file.Tag.Genres = new[] { track.genre };
        if (track.HasArt())
            file.Tag.Pictures = new IPicture[] { new Picture(track.artPath) };
        file.Tag.Track = track.trackNumber;
        file.Tag.TrackCount = track.trackCount;
        file.Tag.Disc = track.discNumber;
        file.Tag.DiscCount = track.discCount;

        file.Save();
    }
    catch (Exception ex) {
        if (File.Exists(path))
            File.Delete(path);
        return Results.Problem(ex.ToString(), null, 500, "Error writing metadata");
    }

    await Results.File(path, file.MimeType, $"{track.artist} - {track.title}{Path.GetExtension(path)}")
        .ExecuteAsync(ctx);

    if (File.Exists(path))
        File.Delete(path);

    return Results.Empty;
});
trackGroup.MapDelete("/{id}", async (HttpContext ctx, ApplicationDbContext db, uint id) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    if (await db.tracks.FindAsync(id) is not { } track)
        return Results.NotFound();

    if (track.username != user.username)
        return Results.Unauthorized();

    string prevAlbumHash = track.albumHash;
    string prevArtPath = track.artPath;

    db.tracks.Remove(track);
    await db.SaveChangesAsync();

    if (File.Exists(prevArtPath) && !db.tracks.Any(x => x.albumHash == prevAlbumHash))
        File.Delete(prevArtPath);

    return Results.NoContent();
});

RouteGroupBuilder playlistGroup = app.MapGroup("/playlist");
playlistGroup.MapPost("/", async (HttpContext ctx, ApplicationDbContext db, Playlist.CreateDto input) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    Playlist playlist = input.ToPlaylist(user.username);
    db.playlists.Add(playlist);
    await db.SaveChangesAsync();
    return Results.Created($"/playlist/{playlist.id}", new Playlist.GetDto(db, playlist));
});
playlistGroup.MapPut("/{id}", async (HttpContext ctx, ApplicationDbContext db, uint id, Playlist.UpdateDto input) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    if (await db.playlists.FindAsync(id) is not { } playlist)
        return Results.NotFound();

    if (playlist.username != user.username)
        return Results.Unauthorized();

    input.MergeInto(playlist);
    await db.SaveChangesAsync();

    return Results.NoContent();
});
playlistGroup.MapPut("/{id}/{trackId}", async (HttpContext ctx, ApplicationDbContext db, uint id, uint trackId) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    if (await db.playlists.FindAsync(id) is not { } playlist)
        return Results.NotFound();

    if (playlist.username != user.username)
        return Results.Unauthorized();

    playlist.tracks = string.IsNullOrWhiteSpace(playlist.tracks) ?
        trackId.ToString() :
        string.Join(',', playlist.tracks.Split(',').Append(trackId.ToString()).ToArray());
    await db.SaveChangesAsync();

    return Results.NoContent();
});
playlistGroup.MapGet("/{id}", async (ApplicationDbContext db, uint id) =>
    await db.playlists.FindAsync(id) is { } playlist ?
        Results.Ok(new Playlist.GetDto(db, playlist)) :
        Results.NotFound());
playlistGroup.MapDelete("/{id}", async (HttpContext ctx, ApplicationDbContext db, uint id) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    if (await db.playlists.FindAsync(id) is not { } playlist)
        return Results.NotFound();

    if (playlist.username != user.username)
        return Results.Unauthorized();

    db.playlists.Remove(playlist);
    await db.SaveChangesAsync();

    return Results.NoContent();
});
playlistGroup.MapDelete("/{id}/{trackId}", async (HttpContext ctx, ApplicationDbContext db, uint id, uint trackId) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    if (await db.playlists.FindAsync(id) is not { } playlist)
        return Results.NotFound();

    if (playlist.username != user.username)
        return Results.Unauthorized();

    playlist.tracks = string.Join(',', playlist.tracks.Split(',').Where(x => x != trackId.ToString()));
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();

return;

static async Task<User?> TryAuthorize(HttpContext ctx, ApplicationDbContext db) {
    string[]? auth = ctx.Request.Headers.Authorization.FirstOrDefault()?.Split(' ');
    return auth is ["Bearer", { } token] ? await db.users.SingleOrDefaultAsync(x => x.token == token) : null;
}
