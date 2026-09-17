using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace colorsin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditoriaYTrazabilidadLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lote_id",
                table: "movimientos_inventario",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observaciones",
                table: "movimientos_inventario",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "auditoria_eventos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    modulo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    accion = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    detalle = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true, collation: "utf8mb4_0900_ai_ci"),
                    fecha = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditoria_eventos", x => x.id);
                    table.ForeignKey(
                        name: "fk_auditoria_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "idx_movinv_lote",
                table: "movimientos_inventario",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "idx_auditoria_modulo_fecha",
                table: "auditoria_eventos",
                columns: new[] { "modulo", "fecha" });

            migrationBuilder.CreateIndex(
                name: "idx_auditoria_usuario",
                table: "auditoria_eventos",
                column: "usuario_id");

            migrationBuilder.AddForeignKey(
                name: "fk_movinv_lote",
                table: "movimientos_inventario",
                column: "lote_id",
                principalTable: "lotes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // -----------------------------------------------------------------
            // INMUTABILIDAD: hoy solo la garantiza la aplicacion.
            //
            // `IAuditoriaService` no expone forma de editar ni borrar eventos,
            // asi que por la API la bitacora es de solo-anexar. Pero cualquiera
            // con acceso directo a MySQL puede reescribirla.
            //
            // El refuerzo real serian dos triggers BEFORE UPDATE / BEFORE
            // DELETE que hagan SIGNAL SQLSTATE '45000'. No van aqui porque
            // crearlos falla con ERROR 1419: con el log binario activo, MySQL
            // exige SUPER para crear triggers, y el usuario `colorsin` no lo
            // tiene (probado: el privilegio SET_USER_ID tampoco alcanza).
            //
            // Habilitarlo pide relajar un flag global del servidor
            // (--log-bin-trust-function-creators=1 en el `command:` del
            // docker-compose) y reiniciar el contenedor. Es una decision de
            // infraestructura, no de esta migracion.
            // -----------------------------------------------------------------
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_movinv_lote",
                table: "movimientos_inventario");

            migrationBuilder.DropTable(
                name: "auditoria_eventos");

            migrationBuilder.DropIndex(
                name: "idx_movinv_lote",
                table: "movimientos_inventario");

            migrationBuilder.DropColumn(
                name: "lote_id",
                table: "movimientos_inventario");

            migrationBuilder.DropColumn(
                name: "observaciones",
                table: "movimientos_inventario");
        }
    }
}
