using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRelacionPerfilSuscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdPerfilCuenta",
                table: "Suscripciones",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suscripciones_IdPerfilCuenta",
                table: "Suscripciones",
                column: "IdPerfilCuenta");

            migrationBuilder.AddForeignKey(
                name: "FK_Suscripciones_PerfilesCuentas_IdPerfilCuenta",
                table: "Suscripciones",
                column: "IdPerfilCuenta",
                principalTable: "PerfilesCuentas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suscripciones_PerfilesCuentas_IdPerfilCuenta",
                table: "Suscripciones");

            migrationBuilder.DropIndex(
                name: "IX_Suscripciones_IdPerfilCuenta",
                table: "Suscripciones");

            migrationBuilder.DropColumn(
                name: "IdPerfilCuenta",
                table: "Suscripciones");
        }
    }
}
