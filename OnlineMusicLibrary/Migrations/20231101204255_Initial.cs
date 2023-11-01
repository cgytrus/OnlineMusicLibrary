using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineMusicLibrary.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "playlists",
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    username = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    tracks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    username = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    artist = table.Column<string>(type: "TEXT", nullable: false),
                    album = table.Column<string>(type: "TEXT", nullable: false),
                    albumArtist = table.Column<string>(type: "TEXT", nullable: false),
                    albumHash = table.Column<string>(type: "TEXT", nullable: false),
                    year = table.Column<uint>(type: "INTEGER", nullable: false),
                    genre = table.Column<string>(type: "TEXT", nullable: false),
                    trackNumber = table.Column<uint>(type: "INTEGER", nullable: false),
                    trackCount = table.Column<uint>(type: "INTEGER", nullable: false),
                    discNumber = table.Column<uint>(type: "INTEGER", nullable: false),
                    discCount = table.Column<uint>(type: "INTEGER", nullable: false),
                    lyrics = table.Column<string>(type: "TEXT", nullable: false),
                    listen = table.Column<string>(type: "TEXT", nullable: false),
                    download = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    username = table.Column<string>(type: "TEXT", nullable: false),
                    token = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.username);
                });

            migrationBuilder.CreateIndex(
                name: "IX_playlists_username",
                table: "playlists",
                column: "username");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_username",
                table: "tracks",
                column: "username");

            migrationBuilder.CreateIndex(
                name: "IX_users_token",
                table: "users",
                column: "token");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playlists");

            migrationBuilder.DropTable(
                name: "tracks");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
