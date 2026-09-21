-- =============================================================================
-- MODULO TRASLADOS: dias de entrega por transportadora
--
-- Anade `transportadoras.dias_entrega`: cuantos dias tarda ESA transportadora en
-- llevar la mercancia de una sede a otra.
--
-- POR QUE HACIA FALTA. La tabla solo decia si el servicio era 'urgente' o
-- 'estandar', y eso es una ETIQUETA, no un plazo: no hay forma de calcular una
-- fecha de llegada a partir de la palabra "urgente". Asi que al despachar habia
-- que teclear la fecha a mano, el campo era opcional, y lo normal era dejarlo
-- vacio. Un traslado en transito sin fecha estimada no se puede reclamar: nadie
-- sabe a partir de cuando va tarde.
--
-- POR QUE UNA COLUMNA Y NO DOS CONSTANTES EN EL CODIGO. Con dos numeros fijos
-- -urgente 1, estandar 3- el dia que una transportadora cambie su plazo habria
-- que recompilar, y los numeros vivirian en el frontend, lejos de los datos que
-- describen. Con columna, se corrige con un UPDATE.
--
-- OJO CON LOS VALORES QUE SIEMBRA ESTE SCRIPT. Son una SUPOSICION, no un dato
-- que nadie haya confirmado:
--
--     urgente   1 dia
--     estandar  3 dias
--
-- Estan puestos para que la pantalla funcione desde el primer dia, no porque
-- sean los plazos reales de estas empresas. Corregidlos con:
--
--     UPDATE transportadoras SET dias_entrega = 2 WHERE id = 1;
--
-- TINYINT UNSIGNED: el plazo es un entero pequeno y no negativo. El CHECK exige
-- ademas que sea al menos 1, porque un traslado que llega el mismo dia que sale
-- no necesita transportadora.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

SET @existe := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'transportadoras'
    AND COLUMN_NAME  = 'dias_entrega'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `transportadoras`
     ADD COLUMN `dias_entrega` TINYINT UNSIGNED NOT NULL DEFAULT 3
     COMMENT ''Dias habiles que tarda en entregar. Alimenta la fecha estimada del despacho''
     AFTER `tipo_servicio`,
     ADD CONSTRAINT `chk_transportadoras_dias_entrega`
       CHECK (`dias_entrega` >= 1)',
  'SELECT ''La columna transportadoras.dias_entrega ya existe'' AS aviso');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Solo sobre las filas que quedaron con el valor por defecto de la columna, para
-- que volver a correr el script no pise un plazo ya corregido a mano.
UPDATE `transportadoras`
   SET `dias_entrega` = 1
 WHERE `tipo_servicio` = 'urgente'
   AND `dias_entrega` = 3;
