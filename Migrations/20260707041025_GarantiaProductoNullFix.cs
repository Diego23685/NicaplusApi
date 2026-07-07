using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class GarantiaProductoNullFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdProducto",
                table: "GarantiasTickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GarantiasTickets_IdProducto",
                table: "GarantiasTickets",
                column: "IdProducto");

            migrationBuilder.AddForeignKey(
                name: "FK_GarantiasTickets_Productos_IdProducto",
                table: "GarantiasTickets",
                column: "IdProducto",
                principalTable: "Productos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GarantiasTickets_Productos_IdProducto",
                table: "GarantiasTickets");

            migrationBuilder.DropIndex(
                name: "IX_GarantiasTickets_IdProducto",
                table: "GarantiasTickets");

            migrationBuilder.DropColumn(
                name: "IdProducto",
                table: "GarantiasTickets");
        }
    }
}
