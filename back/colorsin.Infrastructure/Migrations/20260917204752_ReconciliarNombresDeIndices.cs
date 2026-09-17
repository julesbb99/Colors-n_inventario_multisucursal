using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace colorsin.Infrastructure.Migrations
{
    /// <summary>
    /// Pone de acuerdo los nombres de indice entre el modelo de EF Core y el
    /// esquema fisico.
    ///
    /// EL PROBLEMA
    /// El esquema real se creo con los scripts DDL de infra/mysql/init, que
    /// nombran los indices con prefijo idx_ y abreviaturas (idx_movinv_producto,
    /// idx_ocd_orden). La migracion InitialCreate se marco como aplicada SIN
    /// ejecutarse, asi que la instantanea de EF guardo los nombres que EF habria
    /// puesto: IX_&lt;tabla&gt;_&lt;columna&gt;. Resultado: 23 indices con el mismo
    /// contenido y distinto nombre segun a quien se le pregunte.
    ///
    /// Eso no era cosmetico. Cada migracion que tocaba un indice generaba un
    /// DROP o un RENAME sobre un nombre inexistente, que falla con el error 1091
    /// y deja la migracion a medias. Ya paso una vez, en
    /// LoteUnicoUsuarioOrdenYRecepcionParcial, donde hubo que quitar la
    /// operacion a mano.
    ///
    /// POR QUE NO SE USAN RenameIndex NI CreateIndex
    /// Porque esta migracion tiene que funcionar desde DOS puntos de partida
    /// distintos, y las operaciones de EF solo sirven para uno:
    ///
    ///   - Base creada con los scripts de infra (la que esta en uso): ya tiene
    ///     los nombres idx_ y NO tiene los IX_. Un RenameIndex fallaria.
    ///   - Base creada solo con `dotnet ef database update`: tiene los IX_ y no
    ///     los idx_. Ahi el rename si aplica.
    ///
    /// Con SQL condicional sobre information_schema, cada operacion se ejecuta
    /// solo si hace falta. En la base en uso, esta migracion no cambia nada: se
    /// limita a registrarse en el historial y a dejar la instantanea correcta.
    /// Y es idempotente, asi que volver a correrla tampoco rompe nada.
    /// </summary>
    public partial class ReconciliarNombresDeIndices : Migration
    {
        /// <summary>
        /// Indices que EF nombraba IX_* y en la base se llaman idx_*.
        /// Todos salen de una clave foranea: EF los crea solo, y hasta ahora los
        /// creaba con SU nombre.
        /// </summary>
        private static readonly (string Tabla, string Ef, string Real)[] Renombres =
        [
            ("inventario_sucursal",     "IX_inventario_sucursal_producto_id",          "idx_inventario_producto"),
            ("lotes",                   "IX_lotes_sucursal_id",                        "idx_lotes_sucursal"),
            ("movimientos_inventario",  "IX_movimientos_inventario_producto_id",       "idx_movinv_producto"),
            ("movimientos_inventario",  "IX_movimientos_inventario_unidad_id",         "idx_movinv_unidad"),
            ("movimientos_inventario",  "IX_movimientos_inventario_usuario_id",        "idx_movinv_usuario"),
            ("novedades_transferencia", "IX_novedades_transferencia_transferencia_id", "idx_novtransf_transferencia"),
            ("novedades_transferencia", "IX_novedades_transferencia_usuario_id",       "idx_novtransf_usuario"),
            ("orden_compra_detalle",    "IX_orden_compra_detalle_orden_compra_id",     "idx_ocd_orden"),
            ("orden_compra_detalle",    "IX_orden_compra_detalle_producto_id",         "idx_ocd_producto"),
            ("orden_compra_detalle",    "IX_orden_compra_detalle_unidad_id",           "idx_ocd_unidad"),
            ("ordenes_compra",          "IX_ordenes_compra_proveedor_id",              "idx_oc_proveedor"),
            ("producto_proveedor",      "IX_producto_proveedor_proveedor_id",          "idx_prodprov_proveedor"),
            ("productos",               "IX_productos_unidad_base_id",                 "idx_productos_unidad_base"),
            ("transferencias",          "IX_transferencias_producto_id",               "idx_transf_producto"),
            ("transferencias",          "IX_transferencias_sucursal_origen_id",        "idx_transf_origen"),
            ("transferencias",          "IX_transferencias_transportadora_id",         "idx_transf_transportadora"),
            ("transferencias",          "IX_transferencias_unidad_id",                 "idx_transf_unidad"),
            ("usuarios",                "IX_usuarios_sucursal_id",                     "idx_usuarios_sucursal"),
            ("venta_detalle",           "IX_venta_detalle_producto_id",                "idx_vd_producto"),
            ("venta_detalle",           "IX_venta_detalle_unidad_id",                  "idx_vd_unidad"),
            ("venta_detalle",           "IX_venta_detalle_venta_id",                   "idx_vd_venta"),
            ("ventas",                  "IX_ventas_cliente_id",                        "idx_ventas_cliente"),
            ("ventas",                  "IX_ventas_usuario_id",                        "idx_ventas_usuario")
        ];

        /// <summary>
        /// Indices que estaban en el DDL pero no en el modelo, porque no salen
        /// de ninguna relacion: EF no los deduce. Existen en la base en uso y
        /// faltan en una creada solo con migraciones.
        /// </summary>
        private static readonly (string Tabla, string Nombre, string Columnas)[] QuePuedenFaltar =
        [
            ("lotes",                   "idx_lotes_producto_sucursal", "`producto_id`, `sucursal_id`"),
            ("movimientos_inventario",  "idx_movinv_fecha",            "`fecha`"),
            ("movimientos_inventario",  "idx_movinv_motivo",           "`motivo`"),
            ("novedades_transferencia", "idx_novtransf_tipo",          "`tipo`"),
            ("transferencias",          "idx_transf_estado",           "`estado`"),
            ("ventas",                  "idx_ventas_fecha",            "`fecha`")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (tabla, ef, real) in Renombres)
            {
                migrationBuilder.Sql(RenombrarSiHaceFalta(tabla, ef, real));
            }

            foreach (var (tabla, nombre, columnas) in QuePuedenFaltar)
            {
                migrationBuilder.Sql(CrearSiFalta(tabla, nombre, columnas));
            }

            // Sobrante de una base creada solo con migraciones: InitialCreate
            // creaba este indice para la clave foranea de producto_id, y ahora
            // esa columna la cubre idx_lotes_producto_sucursal. En la base en
            // uso nunca existio, de ahi el borrado condicional.
            migrationBuilder.Sql(BorrarSiExiste("lotes", "IX_lotes_producto_id"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deshace los renombres. No recrea IX_lotes_producto_id: la columna
            // producto_id sigue cubierta por idx_lotes_producto_sucursal y por
            // ux_lotes_producto_sucursal_numero, asi que volver a crearlo seria
            // un indice redundante.
            foreach (var (tabla, ef, real) in Renombres)
            {
                migrationBuilder.Sql(RenombrarSiHaceFalta(tabla, real, ef));
            }

            foreach (var (tabla, nombre, _) in QuePuedenFaltar)
            {
                migrationBuilder.Sql(BorrarSiExiste(tabla, nombre));
            }
        }

        // ---------------------------------------------------------------------
        // Generadores de SQL condicional
        //
        // MySQL no admite IF EXISTS en RENAME INDEX, CREATE INDEX ni DROP INDEX,
        // asi que la condicion se resuelve consultando information_schema y
        // armando la sentencia solo cuando corresponde. `DO 0` es la sentencia
        // vacia cuando no hay nada que hacer.
        //
        // Los nombres se interpolan sin parametrizar porque son constantes de
        // este archivo, no entrada externa: un identificador no se puede pasar
        // como parametro a PREPARE.
        // ---------------------------------------------------------------------

        private static string RenombrarSiHaceFalta(string tabla, string desde, string hacia) => $"""
            SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                            WHERE TABLE_SCHEMA = DATABASE()
                              AND TABLE_NAME = '{tabla}' AND INDEX_NAME = '{desde}');
            SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                             WHERE TABLE_SCHEMA = DATABASE()
                               AND TABLE_NAME = '{tabla}' AND INDEX_NAME = '{hacia}');
            SET @sql := IF(@origen > 0 AND @destino = 0,
                'ALTER TABLE `{tabla}` RENAME INDEX `{desde}` TO `{hacia}`',
                'DO 0');
            PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """;

        private static string CrearSiFalta(string tabla, string nombre, string columnas) => $"""
            SET @existe := (SELECT COUNT(*) FROM information_schema.STATISTICS
                            WHERE TABLE_SCHEMA = DATABASE()
                              AND TABLE_NAME = '{tabla}' AND INDEX_NAME = '{nombre}');
            SET @sql := IF(@existe = 0,
                'CREATE INDEX `{nombre}` ON `{tabla}` ({columnas})',
                'DO 0');
            PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """;

        private static string BorrarSiExiste(string tabla, string nombre) => $"""
            SET @existe := (SELECT COUNT(*) FROM information_schema.STATISTICS
                            WHERE TABLE_SCHEMA = DATABASE()
                              AND TABLE_NAME = '{tabla}' AND INDEX_NAME = '{nombre}');
            SET @sql := IF(@existe > 0,
                'DROP INDEX `{nombre}` ON `{tabla}`',
                'DO 0');
            PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """;
    }
}
