using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationNumberCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationNumberCounters",
                columns: table => new
                {
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationNumberCounters", x => x.Year);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationNumberCounters");
        }
    }
}
