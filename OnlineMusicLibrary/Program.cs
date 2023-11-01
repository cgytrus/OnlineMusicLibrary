using Microsoft.EntityFrameworkCore;

using OnlineMusicLibrary;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

WebApplication app = builder.Build();

RouteGroupBuilder userGroup = app.MapGroup("/user");
userGroup.MapGet("/{username}/tracks", (ApplicationDbContext db, string username) => {
    return db.tracks
        .Where(x => x.username == username)
        .AsEnumerable()
        .Select(x => new Track.GetDto(x))
        .ToArray();
});
userGroup.MapGet("/{username}/playlists", (ApplicationDbContext db, string username) => {
    return db.playlists
        .Where(x => x.username == username)
        .AsEnumerable()
        .Select(x => new Playlist.GetDto(db, x))
        .ToArray();
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
            Results.Ok(lyrics) :
            Results.NotFound():
        Results.NotFound());
trackGroup.MapGet("/{id}/download", async (HttpContext ctx, ApplicationDbContext db, uint id) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    return Results.Ok("todo");
});
trackGroup.MapDelete("/{id}", async (HttpContext ctx, ApplicationDbContext db, uint id) => {
    User? user = await TryAuthorize(ctx, db);
    if (user is null)
        return Results.Unauthorized();

    if (await db.tracks.FindAsync(id) is not { } track)
        return Results.NotFound();

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
    playlist.tracks = string.Join(',', playlist.tracks.Split(',').Append(trackId.ToString()).ToArray());
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