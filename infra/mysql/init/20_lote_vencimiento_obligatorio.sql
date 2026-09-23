-- =============================================================================
-- LOTES: la caducidad pasa a ser obligatoria
--
-- `lotes.fecha_vencimiento` nacio NULL para admitir producto que no caduca. En
-- Colorsin no lo hay: pintura, esmalte y disolvente caducan todos, y el catalogo
-- entero es de esa clase. Lo que la columna permitia de verdad no era registrar
-- un producto eterno, sino recibir una compra sin teclear la fecha.
--
-- QUE ROMPIA ESO. Un lote sin fecha no entra en las alertas de vencimiento -la
-- consulta filtra por `fecha_vencimiento IS NOT NULL`- y en la cola FEFO se va
-- al final, detras de todo lo que si tiene fecha. O sea: el lote que nadie
-- fecho es el ultimo que se despacha y el unico del que nadie avisa. Es
-- exactamente al reves de lo que conviene, porque un lote sin fecha es
-- justamente del que menos se sabe.
--
-- POR QUE EN LA BASE Y NO SOLO EN EL FORMULARIO. El formulario impide teclearlo
-- mal; esto impide guardarlo mal. Son dos cosas distintas: a `lotes` tambien se
-- escribe desde la recepcion de traslados y desde la correccion de un lote, y
-- una regla que vive en una pantalla no cubre las otras dos puertas.
--
-- SE PUEDE APLICAR EN CALIENTE. Al escribir esto las 17 filas existentes tienen
-- fecha, asi que el ALTER no tiene que inventarse ninguna. Si en otra copia de
-- la base hubiera filas sin fecha, el ALTER las convertiria a '0000-00-00' en
-- modo laxo o fallaria en modo estricto; por eso se comprueba ANTES y se aborta
-- con un mensaje en vez de estropear los datos.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

-- --- 1. Ninguna fila sin fecha, o no se sigue -------------------------------
SET @sin_fecha := (
  SELECT COUNT(*) FROM `lotes` WHERE `fecha_vencimiento` IS NULL
);

SET @aviso := CONCAT(
  'No se puede hacer obligatoria la caducidad: hay ', @sin_fecha,
  ' lote(s) sin fecha. Asignesela a cada uno y vuelva a ejecutar este script.'
);

-- SIGNAL no admite una condicion, asi que se simula: si hay filas sin fecha se
-- ejecuta una sentencia que falla a proposito con el aviso como mensaje.
SET @comprobar := IF(
  @sin_fecha = 0,
  'SELECT 1',
  CONCAT('SIGNAL SQLSTATE ''45000'' SET MESSAGE_TEXT = ', QUOTE(@aviso))
);

PREPARE stmt FROM @comprobar;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- --- 2. La columna, a NOT NULL ----------------------------------------------
SET @ya_obligatoria := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'lotes'
    AND COLUMN_NAME  = 'fecha_vencimiento'
    AND IS_NULLABLE  = 'NO'
);

SET @sql := IF(
  @ya_obligatoria = 0,
  'ALTER TABLE `lotes`
     MODIFY COLUMN `fecha_vencimiento` DATE NOT NULL
     COMMENT ''Caducidad del lote. Obligatoria: ordena la cola FEFO y dispara las alertas.''',
  'SELECT 1'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
