using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AddIdVariacionToDetalleCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdVariacion",
                table: "DetallesComprasProveedores",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesComprasProveedores_IdVariacion",
                table: "DetallesComprasProveedores",
                column: "IdVariacion");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesComprasProveedores_VariacionesProductos_IdVariacion",
                table: "DetallesComprasProveedores",
                column: "IdVariacion",
                principalTable: "VariacionesProductos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesComprasProveedores_VariacionesProductos_IdVariacion",
                table: "DetallesComprasProveedores");

            migrationBuilder.DropIndex(
                name: "IX_DetallesComprasProveedores_IdVariacion",
                table: "DetallesComprasProveedores");

            migrationBuilder.DropColumn(
                name: "IdVariacion",
                table: "DetallesComprasProveedores");
        }
    }
}
