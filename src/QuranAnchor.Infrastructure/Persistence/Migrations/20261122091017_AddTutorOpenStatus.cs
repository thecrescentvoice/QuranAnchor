using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuranAnchor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorOpenStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailabilityNote",
                table: "Tutors",
                type: "varchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailabilityUpdatedAtUtc",
                table: "Tutors",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OpenToNewStudents",
                table: "Tutors",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Shifts",
                table: "Tutors",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailabilityNote",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "AvailabilityUpdatedAtUtc",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "OpenToNewStudents",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "Shifts",
                table: "Tutors");
        }
    }
}
