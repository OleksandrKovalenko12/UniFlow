using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrectionDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drafts_StudentId_ApplicationTypeId",
                table: "Drafts");

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "Drafts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "RemovedAttachmentIds",
                table: "Drafts",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_ApplicationId",
                table: "Drafts",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_StudentId",
                table: "Drafts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_StudentId_ApplicationTypeId",
                table: "Drafts",
                columns: new[] { "StudentId", "ApplicationTypeId" },
                unique: true,
                filter: "\"ApplicationId\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Drafts_Applications_ApplicationId",
                table: "Drafts",
                column: "ApplicationId",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Drafts_Applications_ApplicationId",
                table: "Drafts");

            migrationBuilder.DropIndex(
                name: "IX_Drafts_ApplicationId",
                table: "Drafts");

            migrationBuilder.DropIndex(
                name: "IX_Drafts_StudentId",
                table: "Drafts");

            migrationBuilder.DropIndex(
                name: "IX_Drafts_StudentId_ApplicationTypeId",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "RemovedAttachmentIds",
                table: "Drafts");

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_StudentId_ApplicationTypeId",
                table: "Drafts",
                columns: new[] { "StudentId", "ApplicationTypeId" },
                unique: true);
        }
    }
}
