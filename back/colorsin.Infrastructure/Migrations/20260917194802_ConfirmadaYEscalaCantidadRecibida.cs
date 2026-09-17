using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace colorsin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmadaYEscalaCantidadRecibida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "estado",
                table: "ordenes_compra",
                type: "enum('Pendiente','Confirmada','ParcialmenteRecibida','Recibida','Cancelada')",
                nullable: true,
                collation: "utf8mb4_0900_ai_ci",
                oldClrType: typeof(string),
                oldType: "enum('Pendiente','ParcialmenteRecibida','Recibida','Cancelada')",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.AlterColumn<decimal>(
                name: "cantidad_recibida",
                table: "orden_compra_detalle",
                type: "decimal(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,4)",
                oldPrecision: 12,
                oldScale: 4,
                oldDefaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "estado",
                table: "ordenes_compra",
                type: "enum('Pendiente','ParcialmenteRecibida','Recibida','Cancelada')",
                nullable: true,
                collation: "utf8mb4_0900_ai_ci",
                oldClrType: typeof(string),
                oldType: "enum('Pendiente','Confirmada','ParcialmenteRecibida','Recibida','Cancelada')",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.AlterColumn<decimal>(
                name: "cantidad_recibida",
                table: "orden_compra_detalle",
                type: "decimal(12,4)",
                precision: 12,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(14,4)",
                oldPrecision: 14,
                oldScale: 4,
                oldDefaultValue: 0m);
        }
    }
}
