using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rendezvous.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessReviewAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "BusinessReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessReviews_AppointmentId",
                table: "BusinessReviews",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessReviews_Appointments_AppointmentId",
                table: "BusinessReviews",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessReviews_Appointments_AppointmentId",
                table: "BusinessReviews");

            migrationBuilder.DropIndex(
                name: "IX_BusinessReviews_AppointmentId",
                table: "BusinessReviews");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "BusinessReviews");
        }
    }
}
