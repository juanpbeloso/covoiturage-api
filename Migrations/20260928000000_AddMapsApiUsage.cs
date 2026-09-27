using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiteAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddMapsApiUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MapsDirectionsEnabled",
                table: "PlatformSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MapsMonthlyRequestCap",
                table: "PlatformSettings",
                type: "integer",
                nullable: false,
                defaultValue: 10000);

            migrationBuilder.AddColumn<decimal>(
                name: "MapsPricePerThousandUsd",
                table: "PlatformSettings",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 5.00m);

            migrationBuilder.CreateTable(
                name: "ExternalApiUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Sku = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalApiUsages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalApiUsages_Provider_Sku_CreatedAt",
                table: "ExternalApiUsages",
                columns: new[] { "Provider", "Sku", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalApiUsages");

            migrationBuilder.DropColumn(
                name: "MapsDirectionsEnabled",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "MapsMonthlyRequestCap",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "MapsPricePerThousandUsd",
                table: "PlatformSettings");
        }
    }
}
