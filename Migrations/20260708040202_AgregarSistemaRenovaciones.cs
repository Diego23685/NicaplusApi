using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSistemaRenovaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Renovaciones_Clientes_IdCliente",
                table: "Renovaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Renovaciones_Suscripciones_IdSuscripcion",
                table: "Renovaciones");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAsignacion",
                table: "PerfilesCuentas",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaLiberacion",
                table: "PerfilesCuentas",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Renovaciones_Clientes_IdCliente",
                table: "Renovaciones",
                column: "IdCliente",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Renovaciones_Suscripciones_IdSuscripcion",
                table: "Renovaciones",
                column: "IdSuscripcion",
                principalTable: "Suscripciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Renovaciones_Clientes_IdCliente",
                table: "Renovaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Renovaciones_Suscripciones_IdSuscripcion",
                table: "Renovaciones");

            migrationBuilder.DropColumn(
                name: "FechaAsignacion",
                table: "PerfilesCuentas");

            migrationBuilder.DropColumn(
                name: "FechaLiberacion",
                table: "PerfilesCuentas");

            migrationBuilder.AddForeignKey(
                name: "FK_Renovaciones_Clientes_IdCliente",
                table: "Renovaciones",
                column: "IdCliente",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Renovaciones_Suscripciones_IdSuscripcion",
                table: "Renovaciones",
                column: "IdSuscripcion",
                principalTable: "Suscripciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
