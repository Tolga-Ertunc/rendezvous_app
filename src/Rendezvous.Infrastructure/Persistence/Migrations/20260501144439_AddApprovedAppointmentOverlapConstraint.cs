using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rendezvous.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedAppointmentOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("""
                ALTER TABLE "Appointments"
                ADD CONSTRAINT "EX_Appointments_ApprovedStaffTimeOverlap"
                EXCLUDE USING gist (
                    "StaffMemberId" WITH =,
                    tstzrange("StartsAtUtc", "EndsAtUtc", '[)') WITH &&
                )
                WHERE ("Status" = 2);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Appointments"
                DROP CONSTRAINT IF EXISTS "EX_Appointments_ApprovedStaffTimeOverlap";
                """);
        }
    }
}
