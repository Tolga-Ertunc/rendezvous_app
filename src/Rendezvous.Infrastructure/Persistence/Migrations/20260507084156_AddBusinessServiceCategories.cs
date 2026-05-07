using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rendezvous.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessServiceCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessServiceCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessServiceCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessServiceCategories_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessServiceCategories_BusinessId_Name",
                table: "BusinessServiceCategories",
                columns: new[] { "BusinessId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessServiceCategories_BusinessId_SortOrder",
                table: "BusinessServiceCategories",
                columns: new[] { "BusinessId", "SortOrder" });

            migrationBuilder.Sql(
                """
                INSERT INTO "BusinessServiceCategories" ("Id", "BusinessId", "Name", "SortOrder", "IsSystem")
                SELECT gen_random_uuid(), "Id", 'Featured', 0, TRUE
                FROM "Businesses"
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "BusinessServiceCategories"
                    WHERE "BusinessServiceCategories"."BusinessId" = "Businesses"."Id"
                        AND "BusinessServiceCategories"."Name" = 'Featured'
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "BusinessServiceCategories" ("Id", "BusinessId", "Name", "SortOrder", "IsSystem")
                SELECT gen_random_uuid(),
                    "BusinessId",
                    "CategoryName",
                    ROW_NUMBER() OVER (PARTITION BY "BusinessId" ORDER BY "CategoryName"),
                    FALSE
                FROM (
                    SELECT DISTINCT "BusinessId", "CategoryName"
                    FROM "BusinessServices"
                    WHERE "CategoryName" <> 'Featured'
                ) AS service_categories
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "BusinessServiceCategories"
                    WHERE "BusinessServiceCategories"."BusinessId" = service_categories."BusinessId"
                        AND "BusinessServiceCategories"."Name" = service_categories."CategoryName"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessServiceCategories");
        }
    }
}
