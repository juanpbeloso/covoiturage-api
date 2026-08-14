using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubiteAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddConductorMercadoPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConductorMercadoPagos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConductorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MpUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    TokenExpiraEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConectadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConductorMercadoPagos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConductorMercadoPagos_AspNetUsers_ConductorId",
                        column: x => x.ConductorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConductorMercadoPagos_ConductorId",
                table: "ConductorMercadoPagos",
                column: "ConductorId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConductorMercadoPagos");
        }
    }
}
