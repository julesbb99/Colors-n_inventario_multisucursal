using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace colorsin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TransferenciasGuiaYEstados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "transportadora_id",
                table: "transferencias",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "estado",
                table: "transferencias",
                type: "enum('Solicitada','EnTransito','Completada','RecibidaParcial','Rechazada','Cancelada')",
                nullable: true,
                collation: "utf8mb4_0900_ai_ci",
                oldClrType: typeof(string),
                oldType: "enum('Solicitada','EnPreparacion','EnTransito','RecibidaCompleta','RecibidaParcial')",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.AddColumn<string>(
                name: "guia",
                table: "transferencias",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci");

            migrationBuilder.AddColumn<int>(
                name: "transferencia_id",
                table: "movimientos_inventario",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_movinv_transferencia",
                table: "movimientos_inventario",
                column: "transferencia_id");

            migrationBuilder.AddForeignKey(
                name: "fk_movinv_transferencia",
                table: "movimientos_inventario",
                column: "transferencia_id",
                principalTable: "transferencias",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_movinv_transferencia",
                table: "movimientos_inventario");

            migrationBuilder.DropIndex(
                name: "idx_movinv_transferencia",
                table: "movimientos_inventario");

            migrationBuilder.DropColumn(
                name: "guia",
                table: "transferencias");

            migrationBuilder.DropColumn(
                name: "transferencia_id",
                table: "movimientos_inventario");

            migrationBuilder.AlterColumn<int>(
                name: "transportadora_id",
                table: "transferencias",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "estado",
                table: "transferencias",
                type: "enum('Solicitada','EnPreparacion','EnTransito','RecibidaCompleta','RecibidaParcial')",
                nullable: true,
                collation: "utf8mb4_0900_ai_ci",
                oldClrType: typeof(string),
                oldType: "enum('Solicitada','EnTransito','Completada','RecibidaParcial','Rechazada','Cancelada')",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_0900_ai_ci");
        }
    }
}
