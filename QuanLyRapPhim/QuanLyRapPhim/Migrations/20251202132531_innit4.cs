using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyRapPhim.Migrations
{
    /// <inheritdoc />
    public partial class innit4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoucherUsed",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoucherUsed",
                table: "Bookings");
        }
    }
}
