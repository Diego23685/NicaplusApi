using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class PermitirOrdenesServicioSinCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesServicio_Clientes_IdCliente",
                table: "OrdenesServicio");

            migrationBuilder.AlterColumn<int>(
                name: "IdCliente",
                table: "OrdenesServicio",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesServicio_Clientes_IdCliente",
                table: "OrdenesServicio",
                column: "IdCliente",
                principalTable: "Clientes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesServicio_Clientes_IdCliente",
                table: "OrdenesServicio");

            migrationBuilder.AlterColumn<int>(
                name: "IdCliente",
                table: "OrdenesServicio",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesServicio_Clientes_IdCliente",
                table: "OrdenesServicio",
                column: "IdCliente",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
