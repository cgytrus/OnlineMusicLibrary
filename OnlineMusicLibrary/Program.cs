using Microsoft.EntityFrameworkCore;

using OnlineMusicLibrary;

using TagLib;

using YoutubeDLSharp;

using File = System.IO.File;

await YoutubeDLSharp.Utils.DownloadBinaries();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.SchemaGeneratorOptions.SchemaIdSelector = modelType => {
        string parent = modelType.DeclaringType is null ? "" :
            options.SchemaGeneratorOptions.SchemaIdSelector(modelType.DeclaringType);

        string generic = modelType.IsConstructedGenericType ? modelType.GetGenericArguments()
            .Select(genericArg => options.SchemaGeneratorOptions.SchemaIdSelector(genericArg))
            .Aggregate((previous, current) => $"{previous}{current}") : "";

        return $"{parent}{generic}{modelType.Name.Replace("[]", "Array").Split('`').First()}";
    };
});

builder.Services.AddDbContext<ApplicationDbContext>();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin();
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
        policy.WithExposedHeaders("Content-Disposition");
    });
});

WebApplication app = builder.Build();

app.UsePathBase("/music");

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

RouteGroupBuilder baseGroup = app.MapGroup("/v1");

RouteGroupBuilder userGroup = baseGroup.MapGroup("/user");
userGroup.MapGet("/verify", async (HttpContext ctx, ApplicationDbContext db) =>
    await TryAuthorize(ctx, db) is null ? Results.Text("Invalid token") : Results.Text("")).WithOpenApi();
userGroup.MapGet("/{username}/tracks", (ApplicationDbContext db, string username) => {
    return Results.Ok(db.tracks
        .Where(x => x.username == username)
        .AsEnumerable()
        .Select(x => new Track.GetDto(x))
        .ToArray());
}).WithOpenApi();
userGroup.MapGet("/{username}/playlists", async (ApplicationDbContext db, string username) => {
    return Results.Ok(await db.playlists
        .Where(x => x.username == username)
        .Select(x => x.id)
        .ToArrayAsync());
}).WithOpenApi();

RouteGroupBuilder trackGroup = baseGroup.MapGroup("/track");
trackGroup.MapPost("/", async (HttpContext ctx, ApplicationDbContext db, Track.CreateDto input) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    Track track = await input.ToTrack(user.username);
    db.tracks.Add(track);
    await db.SaveChangesAsync();
    return Results.Created($"/track/{track.id}", new Track.GetDto(track));
}).WithOpenApi();
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
}).WithOpenApi();
trackGroup.MapGet("/{id}", async (ApplicationDbContext db, uint id) =>
    await db.tracks.FindAsync(id) is { } track ?
        Results.Ok(new Track.GetDto(track)) :
        Results.NotFound()).WithOpenApi();
trackGroup.MapGet("/{id}/art", async (ApplicationDbContext db, uint id) =>
    await db.tracks.FindAsync(id) is { } track ?
        track.HasArt() ?
            Results.File(track.artPath, "media/jpeg", Path.GetFileName(track.artPath)) :
            Results.NotFound() :
        Results.NotFound()).WithOpenApi();
trackGroup.MapGet("/{id}/lyrics", async (ApplicationDbContext db, uint id) =>
    await db.tracks.FindAsync(id) is { } track ?
        track.lyrics is { } lyrics ?
            Results.Text(lyrics, "text/plain") :
            Results.NotFound() :
        Results.NotFound()).WithOpenApi();
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

    string mimeType = file.MimeType.Replace("taglib", "audio");
    await Results.File(path, mimeType, $"{track.artist} - {track.title}{Path.GetExtension(path)}")
        .ExecuteAsync(ctx);

    if (File.Exists(path))
        File.Delete(path);

    return Results.Empty;
}).WithOpenApi();
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
}).WithOpenApi();

RouteGroupBuilder playlistGroup = baseGroup.MapGroup("/playlist");
playlistGroup.MapPost("/", async (HttpContext ctx, ApplicationDbContext db, Playlist.CreateDto input) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    Playlist playlist = await input.ToPlaylist(db, user.username);
    db.playlists.Add(playlist);
    await db.SaveChangesAsync();
    return Results.Created($"/playlist/{playlist.id}", Playlist.GetDto.From(db, playlist));
}).WithOpenApi();
playlistGroup.MapPut("/{id}", async (HttpContext ctx, ApplicationDbContext db, uint id, Playlist.UpdateDto input) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    if (await db.playlists.FindAsync(id) is not { } playlist)
        return Results.NotFound();

    if (playlist.username != user.username)
        return Results.Unauthorized();

    await input.MergeInto(db, playlist);
    await db.SaveChangesAsync();

    return Results.NoContent();
}).WithOpenApi();
playlistGroup.MapGet("/{id}", async (ApplicationDbContext db, uint id) =>
    await db.playlists.FindAsync(id) is { } playlist ?
        Results.Ok(await Playlist.GetDto.From(db, playlist)) :
        Results.NotFound()).WithOpenApi();
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
}).WithOpenApi();

app.Run();

return;

static async Task<User?> TryAuthorize(HttpContext ctx, ApplicationDbContext db) {
    string[]? auth = ctx.Request.Headers.Authorization.FirstOrDefault()?.Split(' ');
    return auth is ["Bearer", { } token] ? await db.users.SingleOrDefaultAsync(x => x.token == token) : null;
}
