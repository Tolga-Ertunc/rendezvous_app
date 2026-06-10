using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rendezvous.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfilePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerUserId",
                table: "BusinessReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoContentType",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProfilePhotoFileSizeBytes",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProfilePhotoId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoStorageKey",
                table: "AspNetUsers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProfilePhotoUpdatedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "BusinessReviews" AS review
                SET "CustomerUserId" = appointment."CustomerUserId"
                FROM "Appointments" AS appointment
                WHERE review."AppointmentId" = appointment."Id"
                    AND review."CustomerUserId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessReviews_CustomerUserId",
                table: "BusinessReviews",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ProfilePhotoId",
                table: "AspNetUsers",
                column: "ProfilePhotoId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessReviews_AspNetUsers_CustomerUserId",
                table: "BusinessReviews",
                column: "CustomerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessReviews_AspNetUsers_CustomerUserId",
                table: "BusinessReviews");

            migrationBuilder.DropIndex(
                name: "IX_BusinessReviews_CustomerUserId",
                table: "BusinessReviews");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ProfilePhotoId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                table: "BusinessReviews");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoContentType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoFileSizeBytes",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoStorageKey",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoUpdatedAtUtc",
                table: "AspNetUsers");
        }
    }
}
