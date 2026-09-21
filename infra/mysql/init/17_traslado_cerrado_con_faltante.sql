-- =============================================================================
-- MODULO TRASLADOS: estado 'Cerrada' para el traslado que llego corto
--
-- Anade el valor 'Cerrada' al ENUM de `transferencias.estado`.
--
-- EL HUECO QUE TAPA. Un traslado que llegaba corto quedaba en
-- 'RecibidaParcial' y ahi se quedaba PARA SIEMPRE: no habia ninguna accion que
-- lo sacara de ese estado, asi que seguia contando como trabajo en curso
-- aunque ya no hubiera nada que hacer con el. La lista de pendientes se iba
-- llenando de traslados terminados.
--
-- QUIEN LO CIERRA: registrar la novedad que da cuenta del faltante. Ese es el
-- ultimo paso real del traslado -decir que paso con lo que no llego- y con el
-- deja de estar pendiente.
--
-- POR QUE UN ESTADO Y NO DEDUCIRLO DE "TIENE NOVEDADES". Deducirlo parece mas
-- barato y es peor: una novedad de tipo Retraso sobre un traslado que aun
-- viaja tambien lo marcaria como cerrado. El estado dice lo que paso; contar
-- filas de otra tabla es adivinarlo.
--
-- NO ES 'Completada', y la distincion importa: 'Completada' afirma que llego
-- todo. 'Cerrada' dice que se termino de gestionar aunque faltara mercancia, y
-- `cantidad_recibida` sigue guardando cuanto llego de verdad.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

SET @tiene := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'transferencias'
    AND COLUMN_NAME  = 'estado'
    AND COLUMN_TYPE LIKE '%Cerrada%'
);

SET @sql := IF(@tiene = 0,
  'ALTER TABLE `transferencias`
     MODIFY COLUMN `estado`
     ENUM(''Solicitada'',''EnTransito'',''Completada'',''RecibidaParcial'',
          ''Cerrada'',''Rechazada'',''Cancelada'')
     NULL DEFAULT ''Solicitada''',
  'SELECT ''El estado Cerrada ya existe en el ENUM'' AS aviso');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
