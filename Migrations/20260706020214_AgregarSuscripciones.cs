using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSuscripciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Suscripciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdCliente = table.Column<int>(type: "int", nullable: false),
                    NombreServicio = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TipoSuscripcion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdProducto = table.Column<int>(type: "int", nullable: true),
                    IdOrdenServicio = table.Column<int>(type: "int", nullable: true),
                    CostoRenovacion = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Estado = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetallesCredenciales = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suscripciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suscripciones_Clientes_IdCliente",
                        column: x => x.IdCliente,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Suscripciones_OrdenesServicio_IdOrdenServicio",
                        column: x => x.IdOrdenServicio,
                        principalTable: "OrdenesServicio",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Suscripciones_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_IdCliente",
                table: "Suscripciones",
                column: "IdCliente");

            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_IdOrdenServicio",
                table: "Suscripciones",
                column: "IdOrdenServicio");

            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_IdProducto",
                table: "Suscripciones",
                column: "IdProducto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Suscripciones");
        }
    }
}
