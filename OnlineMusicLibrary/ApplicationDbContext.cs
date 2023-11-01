﻿using Microsoft.EntityFrameworkCore;

namespace OnlineMusicLibrary;

public class ApplicationDbContext : DbContext {
    public DbSet<User> users { get; set; } = null!;
    public DbSet<Playlist> playlists { get; set; } = null!;
    public DbSet<Track> tracks { get; set; } = null!;

    private string dbPath { get; } =
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnlineMusicLibrary.db");

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
}
