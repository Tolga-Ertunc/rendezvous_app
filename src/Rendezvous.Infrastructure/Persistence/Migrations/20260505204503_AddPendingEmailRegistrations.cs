using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rendezvous.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEmailRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingEmailRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ConfirmationCodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CodeExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailedAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingEmailRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingEmailRegistrations_CodeExpiresAtUtc",
                table: "PendingEmailRegistrations",
                column: "CodeExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PendingEmailRegistrations_NormalizedEmail",
                table: "PendingEmailRegistrations",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingEmailRegistrations");
        }
    }
}
