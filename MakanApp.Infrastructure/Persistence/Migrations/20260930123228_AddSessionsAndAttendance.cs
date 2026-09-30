using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionsAndAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Enrollments_OrganizationId_ClassId_Id",
                schema: "academic",
                table: "Enrollments",
                columns: new[] { "OrganizationId", "ClassId", "Id" });

            migrationBuilder.CreateTable(
                name: "ScheduleRules",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalDayOfWeek = table.Column<int>(type: "int", nullable: false),
                    LocalStartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MeetingUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRules", x => x.Id);
                    table.UniqueConstraint("UQ_ScheduleRules_OrganizationId_ClassId_Id", x => new { x.OrganizationId, x.ClassId, x.Id });
                    table.CheckConstraint("CK_ScheduleRules_DateRange", "[EffectiveUntil] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ScheduleRules_Duration", "[DurationMinutes] > 0 AND [DurationMinutes] <= 1440");
                    table.CheckConstraint("CK_ScheduleRules_LocalDayOfWeek", "[LocalDayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_ScheduleRules_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ScheduleRules_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurrenceLocalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MeetingUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.UniqueConstraint("UQ_Sessions_OrganizationId_ClassId_Id", x => new { x.OrganizationId, x.ClassId, x.Id });
                    table.CheckConstraint("CK_Sessions_Status", "[Status] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Sessions_TimeRange", "[EndUtc] > [StartUtc]");
                    table.ForeignKey(
                        name: "FK_Sessions_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_ScheduleRules_OrganizationId_ClassId_ScheduleRuleId",
                        columns: x => new { x.OrganizationId, x.ClassId, x.ScheduleRuleId },
                        principalSchema: "academic",
                        principalTable: "ScheduleRules",
                        principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attendance",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RecordedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance", x => x.Id);
                    table.UniqueConstraint("UQ_Attendance_Context_Id", x => new { x.OrganizationId, x.ClassId, x.SessionId, x.EnrollmentId, x.Id });
                    table.CheckConstraint("CK_Attendance_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Attendance_Enrollments_OrganizationId_ClassId_EnrollmentId",
                        columns: x => new { x.OrganizationId, x.ClassId, x.EnrollmentId },
                        principalSchema: "academic",
                        principalTable: "Enrollments",
                        principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendance_Memberships_OrganizationId_RecordedByMembershipId",
                        columns: x => new { x.OrganizationId, x.RecordedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendance_Sessions_OrganizationId_ClassId_SessionId",
                        columns: x => new { x.OrganizationId, x.ClassId, x.SessionId },
                        principalSchema: "academic",
                        principalTable: "Sessions",
                        principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRevisions",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttendanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    CorrectedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CorrectedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRevisions", x => x.Id);
                    table.CheckConstraint("CK_AttendanceRevisions_NewStatus", "[NewStatus] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_AttendanceRevisions_PreviousStatus", "[PreviousStatus] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_AttendanceRevisions_StatusChanged", "[PreviousStatus] <> [NewStatus]");
                    table.ForeignKey(
                        name: "FK_AttendanceRevisions_Attendance_OrganizationId_ClassId_SessionId_EnrollmentId_AttendanceId",
                        columns: x => new { x.OrganizationId, x.ClassId, x.SessionId, x.EnrollmentId, x.AttendanceId },
                        principalSchema: "academic",
                        principalTable: "Attendance",
                        principalColumns: new[] { "OrganizationId", "ClassId", "SessionId", "EnrollmentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRevisions_Memberships_OrganizationId_CorrectedByMembershipId",
                        columns: x => new { x.OrganizationId, x.CorrectedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_OrganizationId_ClassId_EnrollmentId",
                schema: "academic",
                table: "Attendance",
                columns: new[] { "OrganizationId", "ClassId", "EnrollmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_OrganizationId_RecordedByMembershipId",
                schema: "academic",
                table: "Attendance",
                columns: new[] { "OrganizationId", "RecordedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_Attendance_Organization_Session_Enrollment",
                schema: "academic",
                table: "Attendance",
                columns: new[] { "OrganizationId", "SessionId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRevisions_OrganizationId_ClassId_SessionId_EnrollmentId_AttendanceId",
                schema: "academic",
                table: "AttendanceRevisions",
                columns: new[] { "OrganizationId", "ClassId", "SessionId", "EnrollmentId", "AttendanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRevisions_OrganizationId_CorrectedByMembershipId",
                schema: "academic",
                table: "AttendanceRevisions",
                columns: new[] { "OrganizationId", "CorrectedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Organization_Class_Time",
                schema: "academic",
                table: "Sessions",
                columns: new[] { "OrganizationId", "ClassId", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OrganizationId_ClassId_ScheduleRuleId",
                schema: "academic",
                table: "Sessions",
                columns: new[] { "OrganizationId", "ClassId", "ScheduleRuleId" });

            migrationBuilder.CreateIndex(
                name: "UX_Sessions_ScheduleRule_Occurrence",
                schema: "academic",
                table: "Sessions",
                columns: new[] { "OrganizationId", "ScheduleRuleId", "OccurrenceLocalDate" },
                unique: true,
                filter: "[Status] = 1 AND [ScheduleRuleId] IS NOT NULL AND [OccurrenceLocalDate] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRevisions",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Attendance",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "ScheduleRules",
                schema: "academic");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Enrollments_OrganizationId_ClassId_Id",
                schema: "academic",
                table: "Enrollments");
        }
    }
}
