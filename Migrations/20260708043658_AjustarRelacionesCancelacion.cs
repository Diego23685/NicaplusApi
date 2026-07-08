using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AjustarRelacionesCancelacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cancelaciones_Clientes_ClienteId",
                table: "Cancelaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Cancelaciones_Suscripciones_SuscripcionId",
                table: "Cancelaciones");

            migrationBuilder.AlterColumn<int>(
                name: "SuscripcionId",
                table: "Cancelaciones",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ClienteId",
                table: "Cancelaciones",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Cancelaciones_Clientes_ClienteId",
                table: "Cancelaciones",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Cancelaciones_Suscripciones_SuscripcionId",
                table: "Cancelaciones",
                column: "SuscripcionId",
                principalTable: "Suscripciones",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cancelaciones_Clientes_ClienteId",
                table: "Cancelaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Cancelaciones_Suscripciones_SuscripcionId",
                table: "Cancelaciones");

            migrationBuilder.AlterColumn<int>(
                name: "SuscripcionId",
                table: "Cancelaciones",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ClienteId",
                table: "Cancelaciones",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cancelaciones_Clientes_ClienteId",
                table: "Cancelaciones",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cancelaciones_Suscripciones_SuscripcionId",
                table: "Cancelaciones",
                column: "SuscripcionId",
                principalTable: "Suscripciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
