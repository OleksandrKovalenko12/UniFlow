using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationSubmissionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_ApplicantId",
                table: "Applications");

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionKey",
                table: "Applications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ApplicantId_SubmissionKey",
                table: "Applications",
                columns: new[] { "ApplicantId", "SubmissionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_ApplicantId_SubmissionKey",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "SubmissionKey",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ApplicantId",
                table: "Applications",
                column: "ApplicantId");
        }
    }
}
