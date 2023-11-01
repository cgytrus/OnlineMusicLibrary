using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineMusicLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "albumHash",
                table: "tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "albumHash",
                table: "tracks");
        }
    }
}
