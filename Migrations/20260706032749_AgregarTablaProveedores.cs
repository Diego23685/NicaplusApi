using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTablaProveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CuentasPorPagar_Proveedor_IdProveedor",
                table: "CuentasPorPagar");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Proveedor",
                table: "Proveedor");

            migrationBuilder.RenameTable(
                name: "Proveedor",
                newName: "Proveedores");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Proveedores",
                table: "Proveedores",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CuentasPorPagar_Proveedores_IdProveedor",
                table: "CuentasPorPagar",
                column: "IdProveedor",
                principalTable: "Proveedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CuentasPorPagar_Proveedores_IdProveedor",
                table: "CuentasPorPagar");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Proveedores",
                table: "Proveedores");

            migrationBuilder.RenameTable(
                name: "Proveedores",
                newName: "Proveedor");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Proveedor",
                table: "Proveedor",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CuentasPorPagar_Proveedor_IdProveedor",
                table: "CuentasPorPagar",
                column: "IdProveedor",
                principalTable: "Proveedor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
