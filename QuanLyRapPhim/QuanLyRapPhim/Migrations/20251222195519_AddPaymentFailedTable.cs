using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyRapPhim.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFailedTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentFaileds",
                columns: table => new
                {
                    PaymentFailedId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VnPayResponseCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShowtimeId = table.Column<int>(type: "int", nullable: true),
                    MovieTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoomName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShowTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    SelectedSeatsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedFoodsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VoucherUsed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentFaileds", x => x.PaymentFailedId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentFaileds");
        }
    }
}
