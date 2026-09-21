-- =============================================================================
-- MODULO TRASLADOS: baja logica de transportadoras
--
-- Anade `transportadoras.activo`. Una transportadora retirada deja de ofrecerse
-- al despachar, pero NO se borra: los traslados que llevo siguen citandola, con
-- su guia y su fecha estimada.
--
-- POR QUE NO UN DELETE DE VERDAD. `transferencias.transportadora_id` la
-- referencia, asi que en cuanto una transportadora tenga UN traslado, MySQL
-- rechaza el borrado o -peor, segun como este declarada la FK- se lleva por
-- delante el dato de quien transporto la carga. Y ese dato es justo el que hace
-- falta el dia que se reclama un faltante: sin el no se sabe a quien reclamarle.
--
-- La operacion funcionaria solo con transportadoras recien creadas y fallaria
-- con las que llevan anos de historia, que es al reves de lo util.
--
-- ES REVERSIBLE a proposito: se vuelve a contratar a una empresa que se habia
-- dejado de usar, y reactivarla conserva su historia en vez de crear un
-- duplicado con el mismo nombre.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

SET @existe := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'transportadoras'
    AND COLUMN_NAME  = 'activo'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `transportadoras`
     ADD COLUMN `activo` TINYINT(1) NOT NULL DEFAULT 1
     COMMENT ''0 = retirada: no se ofrece al despachar, conserva su historia''
     AFTER `dias_entrega`',
  'SELECT ''La columna transportadoras.activo ya existe'' AS aviso');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
