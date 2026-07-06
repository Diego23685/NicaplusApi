using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class SistemaContabilidadMovimientosCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovimientosCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Fecha = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Tipo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Concepto = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Detalle = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdVenta = table.Column<int>(type: "int", nullable: true),
                    IdCompraProveedor = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_ComprasProveedores_IdCompraProveedor",
                        column: x => x.IdCompraProveedor,
                        principalTable: "ComprasProveedores",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Ventas_IdVenta",
                        column: x => x.IdVenta,
                        principalTable: "Ventas",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_IdCompraProveedor",
                table: "MovimientosCaja",
                column: "IdCompraProveedor");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_IdVenta",
                table: "MovimientosCaja",
                column: "IdVenta");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosCaja");
        }
    }
}
