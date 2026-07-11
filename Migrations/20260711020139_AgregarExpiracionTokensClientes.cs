using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AgregarExpiracionTokensClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiracionTokenConfirmacion",
                table: "Clientes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiracionTokenRecuperacion",
                table: "Clientes",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiracionTokenConfirmacion",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "ExpiracionTokenRecuperacion",
                table: "Clientes");
        }
    }
}
