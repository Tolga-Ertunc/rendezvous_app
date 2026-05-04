using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rendezvous.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilityExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvailabilityExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsFullDay = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndsAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityExceptions", x => x.Id);
                    table.CheckConstraint("CK_AvailabilityExceptions_TimeRange", "((\"IsFullDay\" = TRUE AND \"StartsAt\" IS NULL AND \"EndsAt\" IS NULL)\nOR (\"IsFullDay\" = FALSE AND \"StartsAt\" IS NOT NULL AND \"EndsAt\" IS NOT NULL AND \"StartsAt\" < \"EndsAt\"))");
                    table.CheckConstraint("CK_AvailabilityExceptions_TypeScope", "((\"Type\" IN (1, 2) AND \"StaffMemberId\" IS NULL)\nOR (\"Type\" = 3 AND \"StaffMemberId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_AvailabilityExceptions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvailabilityExceptions_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvailabilityExceptions_StaffMembers_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityExceptions_BusinessId_Date_StaffMemberId",
                table: "AvailabilityExceptions",
                columns: new[] { "BusinessId", "Date", "StaffMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityExceptions_CreatedByUserId",
                table: "AvailabilityExceptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityExceptions_StaffMemberId",
                table: "AvailabilityExceptions",
                column: "StaffMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvailabilityExceptions");
        }
    }
}
