using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOneApplicationInProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Applications_EnrollmentId_ApplicationTypeId_InProgress",
                table: "Applications",
                columns: new[] { "EnrollmentId", "ApplicationTypeId" },
                unique: true,
                filter: "\"Status\" IN (1, 2, 3, 4, 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_EnrollmentId_ApplicationTypeId_InProgress",
                table: "Applications");
        }
    }
}
