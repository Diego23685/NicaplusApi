using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class ModuloProveedoresYCompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComprasProveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdProveedor = table.Column<int>(type: "int", nullable: false),
                    FechaCompra = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TotalCompra = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TiempoEntregaRealDias = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasProveedores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprasProveedores_Proveedores_IdProveedor",
                        column: x => x.IdProveedor,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DetallesComprasProveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdCompraProveedor = table.Column<int>(type: "int", nullable: false),
                    IdProducto = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GarantiaDiasPactada = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesComprasProveedores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesComprasProveedores_ComprasProveedores_IdCompraProvee~",
                        column: x => x.IdCompraProveedor,
                        principalTable: "ComprasProveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesComprasProveedores_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasProveedores_IdProveedor",
                table: "ComprasProveedores",
                column: "IdProveedor");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesComprasProveedores_IdCompraProveedor",
                table: "DetallesComprasProveedores",
                column: "IdCompraProveedor");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesComprasProveedores_IdProducto",
                table: "DetallesComprasProveedores",
                column: "IdProducto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesComprasProveedores");

            migrationBuilder.DropTable(
                name: "ComprasProveedores");
        }
    }
}
