using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace colorsin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoteUnicoUsuarioOrdenYRecepcionParcial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----------------------------------------------------------------
            // EF genero aqui un DropIndex de `IX_lotes_producto_id`, que se
            // quito a mano. Ese indice NO existe en la base: el esquema real se
            // creo con los scripts DDL de infra/mysql/init, donde el indice
            // sobre producto_id forma parte del compuesto
            // `idx_lotes_producto_sucursal`. EF cree que existe porque la
            // migracion InitialCreate se marco como aplicada sin ejecutarse,
            // asi que su instantanea guarda los nombres que EF habria puesto,
            // no los que realmente hay.
            //
            // Ejecutarlo fallaria con el error 1091 ("Can't DROP ...; check that
            // it exists") y dejaria la migracion a medias.
            //
            // Es un caso de un problema mas amplio: 23 indices se llaman
            // distinto de un lado y del otro. Solo este estorba hoy, porque es
            // el unico que esta migracion toca.
            // ----------------------------------------------------------------

            migrationBuilder.AlterColumn<string>(
                name: "estado",
                table: "ordenes_compra",
                type: "enum('Pendiente','ParcialmenteRecibida','Recibida','Cancelada')",
                nullable: true,
                collation: "utf8mb4_0900_ai_ci",
                oldClrType: typeof(string),
                oldType: "enum('Pendiente','Confirmada','Recibida','Cancelada')",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            // Sin `defaultValue: 0`, que es lo que EF genera para rellenar filas
            // existentes: `ordenes_compra` esta vacia, y dejar DEFAULT 0 sobre
            // una columna que es clave foranea sugiere que el cero es un valor
            // valido cuando no lo es (ningun usuario tiene id 0). El modelo
            // tampoco declara ese default, asi que lo dejaria desincronizado.
            migrationBuilder.AddColumn<int>(
                name: "usuario_id",
                table: "ordenes_compra",
                type: "int",
                nullable: false);

            migrationBuilder.AddColumn<decimal>(
                name: "cantidad_recibida",
                table: "orden_compra_detalle",
                type: "decimal(12,4)",
                precision: 12,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "idx_oc_usuario",
                table: "ordenes_compra",
                column: "usuario_id");

            migrationBuilder.AddCheckConstraint(
                name: "chk_ocd_cantidad_recibida",
                table: "orden_compra_detalle",
                sql: "`cantidad_recibida` >= 0 AND (`cantidad` IS NULL OR `cantidad_recibida` <= `cantidad`)");

            migrationBuilder.CreateIndex(
                name: "ux_lotes_producto_sucursal_numero",
                table: "lotes",
                columns: new[] { "producto_id", "sucursal_id", "numero_lote" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_oc_usuario",
                table: "ordenes_compra",
                column: "usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_oc_usuario",
                table: "ordenes_compra");

            migrationBuilder.DropIndex(
                name: "idx_oc_usuario",
                table: "ordenes_compra");

            migrationBuilder.DropCheckConstraint(
                name: "chk_ocd_cantidad_recibida",
                table: "orden_compra_detalle");

            migrationBuilder.DropIndex(
                name: "ux_lotes_producto_sucursal_numero",
                table: "lotes");

            migrationBuilder.DropColumn(
                name: "usuario_id",
                table: "ordenes_compra");

            migrationBuilder.DropColumn(
                name: "cantidad_recibida",
                table: "orden_compra_detalle");

            migrationBuilder.AlterColumn<string>(
                name: "estado",
                table: "ordenes_compra",
                type: "enum('Pendiente','Confirmada','Recibida','Cancelada')",
                nullable: true,
                collation: "utf8mb4_0900_ai_ci",
                oldClrType: typeof(string),
                oldType: "enum('Pendiente','ParcialmenteRecibida','Recibida','Cancelada')",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_lotes_producto_id",
                table: "lotes",
                column: "producto_id");
        }
    }
}
