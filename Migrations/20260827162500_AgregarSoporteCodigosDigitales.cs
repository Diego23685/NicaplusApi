using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSoporteCodigosDigitales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodigosDigitales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdProducto = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    IdVariacion = table.Column<int>(type: "int", nullable: true),
                    VariacionId = table.Column<int>(type: "int", nullable: true),
                    Clave = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Vendido = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Estado = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaVenta = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IdVenta = table.Column<int>(type: "int", nullable: true),
                    IdClienteAsignado = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosDigitales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodigosDigitales_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CodigosDigitales_VariacionesProductos_VariacionId",
                        column: x => x.VariacionId,
                        principalTable: "VariacionesProductos",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CodigosDigitales_ProductoId",
                table: "CodigosDigitales",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_CodigosDigitales_VariacionId",
                table: "CodigosDigitales",
                column: "VariacionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodigosDigitales");
        }
    }
}
