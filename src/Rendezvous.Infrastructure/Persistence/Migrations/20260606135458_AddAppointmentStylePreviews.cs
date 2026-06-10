using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rendezvous.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentStylePreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentStylePreviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    GeneratedStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    GeneratedContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GeneratedFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsPlaceholder = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentStylePreviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentStylePreviews_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AppointmentStylePreviews_AspNetUsers_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentStylePreviews_BusinessServices_BusinessServiceId",
                        column: x => x.BusinessServiceId,
                        principalTable: "BusinessServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentStylePreviews_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentStylePreviews_StaffMembers_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStylePreviews_AppointmentId",
                table: "AppointmentStylePreviews",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStylePreviews_BusinessId",
                table: "AppointmentStylePreviews",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStylePreviews_BusinessServiceId",
                table: "AppointmentStylePreviews",
                column: "BusinessServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStylePreviews_CustomerUserId",
                table: "AppointmentStylePreviews",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStylePreviews_StaffMemberId",
                table: "AppointmentStylePreviews",
                column: "StaffMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentStylePreviews");
        }
    }
}
