using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AddRenovacionToMovimientoCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_IdRenovacion",
                table: "MovimientosCaja",
                column: "IdRenovacion");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCaja_Renovaciones_IdRenovacion",
                table: "MovimientosCaja",
                column: "IdRenovacion",
                principalTable: "Renovaciones",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCaja_Renovaciones_IdRenovacion",
                table: "MovimientosCaja");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_IdRenovacion",
                table: "MovimientosCaja");
        }
    }
}
