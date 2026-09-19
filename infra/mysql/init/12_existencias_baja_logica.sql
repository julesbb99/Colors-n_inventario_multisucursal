-- =============================================================================
-- MODULO INVENTARIO: baja logica de una existencia
--
-- Anade `inventario_sucursal.activo`. Una existencia deshabilitada deja de
-- listarse y de disparar alertas, pero NO se borra: su fila, su saldo y todo el
-- libro mayor que la referencia siguen ahi.
--
-- POR QUE BAJA LOGICA Y NO DELETE. La fila de saldo es el punto de union entre
-- una sede y un producto, y de ella cuelgan lotes, movimientos, lineas de venta
-- y recepciones de compra. Un DELETE tendria que elegir entre romper esas
-- referencias o arrastrarlas, y las dos opciones destruyen historia: el libro
-- mayor dejaria de cuadrar con los documentos que lo originaron. Deshabilitar
-- resuelve lo que se pide de verdad -que el producto deje de aparecer en esa
-- sede- sin tocar nada de lo ya ocurrido.
--
-- QUIEN Y CUANDO NO SE GUARDAN AQUI. Van a `auditoria_eventos`, que es la tabla
-- que ya registra quien hizo que en todos los modulos. Repetir un
-- `desactivado_por` en esta tabla crearia una segunda version de la misma
-- verdad, y solo serviria para la ultima baja: si algo se deshabilita, se
-- reactiva y se vuelve a deshabilitar, la bitacora conserva los tres pasos y una
-- columna solo conserva el ultimo.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

SET @existe := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'inventario_sucursal'
    AND COLUMN_NAME  = 'activo'
);

-- DEFAULT 1 y NOT NULL: las filas que ya existen quedan activas, que es lo que
-- eran hasta ahora. Un nulo aqui significaria "no se sabe si esta activa", y esa
-- duda no existe en el negocio.
SET @sql := IF(@existe = 0,
  'ALTER TABLE `inventario_sucursal`
     ADD COLUMN `activo` TINYINT(1) NOT NULL DEFAULT 1
     COMMENT ''0 = deshabilitada: no se lista ni alerta, pero conserva saldo e historia''
     AFTER `costo_promedio`',
  'SELECT ''La columna inventario_sucursal.activo ya existe'' AS aviso');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Indice por (sucursal, activo): el listado de existencias filtra siempre por
-- sede y casi siempre por activas, y esa pareja es la que lo resuelve sin
-- recorrer la tabla. El UNIQUE (sucursal_id, producto_id) NO se toca: sigue
-- habiendo una sola fila por pareja, este o no activa, que es lo que impide
-- duplicar un producto en una sede deshabilitandolo y volviendolo a crear.
SET @existeIdx := (
  SELECT COUNT(*)
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'inventario_sucursal'
    AND INDEX_NAME   = 'idx_inventario_sucursal_activo'
);

SET @sqlIdx := IF(@existeIdx = 0,
  'CREATE INDEX `idx_inventario_sucursal_activo`
     ON `inventario_sucursal` (`sucursal_id`, `activo`)',
  'SELECT ''El indice idx_inventario_sucursal_activo ya existe'' AS aviso');

PREPARE stmtIdx FROM @sqlIdx;
EXECUTE stmtIdx;
DEALLOCATE PREPARE stmtIdx;
