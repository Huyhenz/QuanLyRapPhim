using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyRapPhim.Migrations
{
    /// <inheritdoc />
    public partial class init6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "Showtimes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_RoomId",
                table: "Showtimes",
                column: "RoomId");

            migrationBuilder.AddForeignKey(
            name: "FK_Showtimes_Rooms_RoomId",
            table: "Showtimes",
            column: "RoomId",
            principalTable: "Rooms",
            principalColumn: "RoomId",
            onDelete: ReferentialAction.Restrict);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Showtimes_Rooms_RoomId",
                table: "Showtimes");

            migrationBuilder.DropIndex(
                name: "IX_Showtimes_RoomId",
                table: "Showtimes");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Showtimes");
        }
    }
}
