using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRelacionVentaSuscripcions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Suscripciones_IdSuscripcion",
                table: "Ventas");

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Suscripciones_IdSuscripcion",
                table: "Ventas",
                column: "IdSuscripcion",
                principalTable: "Suscripciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Suscripciones_IdSuscripcion",
                table: "Ventas");

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Suscripciones_IdSuscripcion",
                table: "Ventas",
                column: "IdSuscripcion",
                principalTable: "Suscripciones",
                principalColumn: "Id");
        }
    }
}
