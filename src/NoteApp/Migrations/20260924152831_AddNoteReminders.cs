using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteApp.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RemindAt",
                table: "Notes",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemindAt",
                table: "Notes");
        }
    }
}
