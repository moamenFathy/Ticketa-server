using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexesForShowtime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Showtimes_HallId",
                table: "Showtimes");

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_HallId_StartTime",
                table: "Showtimes",
                columns: new[] { "HallId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_IsArchived_Status_MovieId",
                table: "Showtimes",
                columns: new[] { "IsArchived", "Status", "MovieId" },
                filter: "[IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Title",
                table: "Movies",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Showtimes_HallId_StartTime",
                table: "Showtimes");

            migrationBuilder.DropIndex(
                name: "IX_Showtimes_IsArchived_Status_MovieId",
                table: "Showtimes");

            migrationBuilder.DropIndex(
                name: "IX_Movies_Title",
                table: "Movies");

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_HallId",
                table: "Showtimes",
                column: "HallId");
        }
    }
}
