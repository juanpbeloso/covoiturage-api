using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiteAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRideStopsAndSegmentReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TotalDistanceKm",
                table: "Rides",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoardingCity",
                table: "Reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlightingCity",
                table: "Reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BoardingSequence",
                table: "Reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AlightingSequence",
                table: "Reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RideStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Lat = table.Column<double>(type: "double precision", nullable: true),
                    Lng = table.Column<double>(type: "double precision", nullable: true),
                    DistanceFromOriginKm = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideStops_Rides_RideId",
                        column: x => x.RideId,
                        principalTable: "Rides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RideStops_RideId_Sequence",
                table: "RideStops",
                columns: new[] { "RideId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RideStops");

            migrationBuilder.DropColumn(name: "TotalDistanceKm", table: "Rides");
            migrationBuilder.DropColumn(name: "BoardingCity", table: "Reservations");
            migrationBuilder.DropColumn(name: "AlightingCity", table: "Reservations");
            migrationBuilder.DropColumn(name: "BoardingSequence", table: "Reservations");
            migrationBuilder.DropColumn(name: "AlightingSequence", table: "Reservations");
        }
    }
}
