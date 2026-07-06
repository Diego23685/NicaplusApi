using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicaplusApi.Migrations
{
    /// <inheritdoc />
    public partial class AddStreamingCatalogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Productos",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "GarantiaDias",
                table: "Productos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Proveedor",
                table: "Productos",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "GarantiaDias",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Proveedor",
                table: "Productos");
        }
    }
}
