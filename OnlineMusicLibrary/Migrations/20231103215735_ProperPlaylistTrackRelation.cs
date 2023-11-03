using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineMusicLibrary.Migrations
{
    /// <inheritdoc />
    public partial class ProperPlaylistTrackRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tracks",
                table: "playlists");

            migrationBuilder.CreateTable(
                name: "playlistTracks",
                columns: table => new
                {
                    position = table.Column<uint>(type: "INTEGER", nullable: false),
                    playlistId = table.Column<uint>(type: "INTEGER", nullable: false),
                    trackId = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlistTracks", x => new { x.position, x.playlistId });
                    table.ForeignKey(
                        name: "FK_playlistTracks_playlists_playlistId",
                        column: x => x.playlistId,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_playlistTracks_tracks_trackId",
                        column: x => x.trackId,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_playlistTracks_playlistId",
                table: "playlistTracks",
                column: "playlistId");

            migrationBuilder.CreateIndex(
                name: "IX_playlistTracks_trackId",
                table: "playlistTracks",
                column: "trackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playlistTracks");

            migrationBuilder.AddColumn<string>(
                name: "tracks",
                table: "playlists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
