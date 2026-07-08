using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRelacionVentaSuscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdSuscripcion",
                table: "Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_IdSuscripcion",
                table: "Ventas",
                column: "IdSuscripcion");

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Suscripciones_IdSuscripcion",
                table: "Ventas",
                column: "IdSuscripcion",
                principalTable: "Suscripciones",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Suscripciones_IdSuscripcion",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_IdSuscripcion",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "IdSuscripcion",
                table: "Ventas");
        }
    }
}
