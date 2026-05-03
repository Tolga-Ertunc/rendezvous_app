using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rendezvous.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyWorkingHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessWorkingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ClosesAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessWorkingHours", x => x.Id);
                    table.CheckConstraint("CK_BusinessWorkingHours_DayOfWeek", "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6");
                    table.CheckConstraint("CK_BusinessWorkingHours_TimeRange", "\"OpensAt\" < \"ClosesAt\"");
                    table.ForeignKey(
                        name: "FK_BusinessWorkingHours_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffWorkingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndsAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffWorkingHours", x => x.Id);
                    table.CheckConstraint("CK_StaffWorkingHours_DayOfWeek", "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6");
                    table.CheckConstraint("CK_StaffWorkingHours_TimeRange", "\"StartsAt\" < \"EndsAt\"");
                    table.ForeignKey(
                        name: "FK_StaffWorkingHours_StaffMembers_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWorkingHours_BusinessId_DayOfWeek",
                table: "BusinessWorkingHours",
                columns: new[] { "BusinessId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkingHours_StaffMemberId_DayOfWeek",
                table: "StaffWorkingHours",
                columns: new[] { "StaffMemberId", "DayOfWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessWorkingHours");

            migrationBuilder.DropTable(
                name: "StaffWorkingHours");
        }
    }
}
