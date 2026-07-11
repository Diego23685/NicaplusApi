using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class LogAuditoriaClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdCliente",
                table: "LogsAuditoria",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoActor",
                table: "LogsAuditoria",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "Clientes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmado",
                table: "Clientes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Clientes",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TokenConfirmacion",
                table: "Clientes",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TokenRecuperacion",
                table: "Clientes",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoAcceso",
                table: "Clientes",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdCliente",
                table: "LogsAuditoria");

            migrationBuilder.DropColumn(
                name: "TipoActor",
                table: "LogsAuditoria");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "EmailConfirmado",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "TokenConfirmacion",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "TokenRecuperacion",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "UltimoAcceso",
                table: "Clientes");
        }
    }
}
