using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuranAnchor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanRows");

            migrationBuilder.DropColumn(
                name: "ClassFormat",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "DailyTilawahFocus",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Goal",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MonthGoals",
                table: "Plans");

            migrationBuilder.AddColumn<Guid>(
                name: "SupervisorId",
                table: "Tutors",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<decimal>(
                name: "ClassFeeAmount",
                table: "Students",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ClassFeeCurrency",
                table: "Students",
                type: "varchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "NGN",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SupervisorId",
                table: "Students",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "FlyerKind",
                table: "Announcements",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "FlyerName",
                table: "Announcements",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "FlyerPath",
                table: "Announcements",
                type: "varchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Announcements",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Announcements",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TutorId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    Note = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_unicode_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Availability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Availability_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Enquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ParentName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Phone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, collation: "utf8mb4_unicode_ci"),
                    StudentName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_unicode_ci"),
                    StudentAge = table.Column<int>(type: "int", nullable: true),
                    Country = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_unicode_ci"),
                    TimeZoneId = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Level = table.Column<int>(type: "int", nullable: false),
                    PreferredTimes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Message = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_unicode_ci"),
                    TrialStartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TrialTutorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TrialMeetingLink = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_unicode_ci"),
                    SupervisorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enquiries", x => x.Id);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    StudentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Number = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsCancelled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Agenda = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false, collation: "utf8mb4_unicode_ci"),
                    StartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false, collation: "utf8mb4_unicode_ci"),
                    MeetingLink = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_unicode_ci"),
                    AllTutors = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Notes = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false, collation: "utf8mb4_unicode_ci"),
                    IsCancelled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_unicode_ci"),
                    SupervisorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "PlanGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PlanId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Week = table.Column<int>(type: "int", nullable: false),
                    Track = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_unicode_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanGoals_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "TutorRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    StudentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TutorId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Stars = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false, collation: "utf8mb4_unicode_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorRatings_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorRatings_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "InvoicePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    InvoiceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci"),
                    RecordedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_unicode_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePayments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "MeetingInvitees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MeetingId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TutorId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingInvitees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingInvitees_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingInvitees_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Availability_TutorId",
                table: "Availability",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_Enquiries_CreatedAtUtc",
                table: "Enquiries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_InvoiceId",
                table: "InvoicePayments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                table: "Invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_StudentId",
                table: "Invoices",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingInvitees_MeetingId_TutorId",
                table: "MeetingInvitees",
                columns: new[] { "MeetingId", "TutorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingInvitees_TutorId",
                table: "MeetingInvitees",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_StartUtc",
                table: "Meetings",
                column: "StartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PlanGoals_PlanId_Week_Track",
                table: "PlanGoals",
                columns: new[] { "PlanId", "Week", "Track" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorRatings_StudentId_Year_Month",
                table: "TutorRatings",
                columns: new[] { "StudentId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorRatings_TutorId",
                table: "TutorRatings",
                column: "TutorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Availability");

            migrationBuilder.DropTable(
                name: "Enquiries");

            migrationBuilder.DropTable(
                name: "InvoicePayments");

            migrationBuilder.DropTable(
                name: "MeetingInvitees");

            migrationBuilder.DropTable(
                name: "PlanGoals");

            migrationBuilder.DropTable(
                name: "TutorRatings");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropColumn(
                name: "SupervisorId",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "ClassFeeAmount",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ClassFeeCurrency",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "SupervisorId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FlyerKind",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "FlyerName",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "FlyerPath",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Announcements");

            migrationBuilder.AddColumn<string>(
                name: "ClassFormat",
                table: "Plans",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "DailyTilawahFocus",
                table: "Plans",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "Goal",
                table: "Plans",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "MonthGoals",
                table: "Plans",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "PlanRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PlanId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Activity = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Content = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Murajaah = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false, collation: "utf8mb4_unicode_ci"),
                    SessionFrom = table.Column<int>(type: "int", nullable: false),
                    SessionTo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanRows_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_PlanRows_PlanId",
                table: "PlanRows",
                column: "PlanId");
        }
    }
}
