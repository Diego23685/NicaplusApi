using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AddVariacionesProductos2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VariacionId",
                table: "DetallesVentas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVentas_VariacionId",
                table: "DetallesVentas",
                column: "VariacionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesVentas_VariacionesProductos_VariacionId",
                table: "DetallesVentas",
                column: "VariacionId",
                principalTable: "VariacionesProductos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesVentas_VariacionesProductos_VariacionId",
                table: "DetallesVentas");

            migrationBuilder.DropIndex(
                name: "IX_DetallesVentas_VariacionId",
                table: "DetallesVentas");

            migrationBuilder.DropColumn(
                name: "VariacionId",
                table: "DetallesVentas");
        }
    }
}
