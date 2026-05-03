using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rendezvous.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationUserPublicNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PublicNumber",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET "PublicNumber" = 10000000 + existing_users."RowNumber" - 1
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (ORDER BY "Id") AS "RowNumber"
                    FROM "AspNetUsers"
                    WHERE "PublicNumber" IS NULL
                ) AS existing_users
                WHERE "AspNetUsers"."Id" = existing_users."Id";
                """);

            migrationBuilder.AlterColumn<int>(
                name: "PublicNumber",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PublicNumber",
                table: "AspNetUsers",
                column: "PublicNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_PublicNumber_8Digits",
                table: "AspNetUsers",
                sql: "\"PublicNumber\" >= 10000000 AND \"PublicNumber\" <= 99999999");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PublicNumber",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_PublicNumber_8Digits",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PublicNumber",
                table: "AspNetUsers");
        }
    }
}
