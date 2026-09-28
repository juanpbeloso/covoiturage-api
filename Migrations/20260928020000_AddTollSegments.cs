using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiteAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTollSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TollSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Corridor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FromCity = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ToCity = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TollSegments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TollSegments_Corridor_Sequence",
                table: "TollSegments",
                columns: new[] { "Corridor", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TollSegments");
        }
    }
}
