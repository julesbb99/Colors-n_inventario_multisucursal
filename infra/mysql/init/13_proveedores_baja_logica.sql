-- =============================================================================
-- MODULO COMPRAS: baja logica de proveedores
--
-- Anade `proveedores.activo`. Un proveedor deshabilitado deja de ofrecerse al
-- crear una orden, pero NO se borra: sus ordenes historicas, su lista de precios
-- y todo lo que cuelga de el siguen ahi.
--
-- POR QUE NO UN DELETE DE VERDAD. Hay dos motivos y los dos bastan por si solos:
--
--   1. `producto_proveedor` referencia al proveedor con ON DELETE CASCADE, asi
--      que borrarlo se llevaria por delante su lista de precios sin avisar. Ahi
--      esta lo que costaba cada producto, que es justo lo que se consulta al
--      pedir de nuevo.
--   2. `ordenes_compra` lo referencia con ON DELETE RESTRICT, asi que en cuanto
--      un proveedor tenga UNA orden, MySQL rechaza el borrado. La operacion
--      funcionaria solo con proveedores nuevos y fallaria justo con los que
--      llevan anos de historia, que es al reves de lo util.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

SET @existe := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'proveedores'
    AND COLUMN_NAME  = 'activo'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `proveedores`
     ADD COLUMN `activo` TINYINT(1) NOT NULL DEFAULT 1
     COMMENT ''0 = deshabilitado: no se ofrece en ordenes nuevas, conserva historia y precios''
     AFTER `telefono`',
  'SELECT ''La columna proveedores.activo ya existe'' AS aviso');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
