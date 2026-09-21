-- =============================================================================
-- MODULO TRASLADOS: cantidad despachada, fechas del ciclo y tratamiento de las
-- novedades.
--
-- Son tres huecos distintos que se tapan juntos porque los tres hacen falta
-- para lo mismo: poder decir si un traslado se cumplio.
--
-- -----------------------------------------------------------------------------
-- 1. `cantidad_despachada`: lo que de verdad salio del origen
--
-- Hasta ahora el despacho sacaba SIEMPRE `cantidad_solicitada`, asi que la sede
-- origen solo podia atender el traslado entero o rechazarlo. En la practica lo
-- normal es lo intermedio -piden 5, hay 3- y no habia como decirlo: o se
-- despachaban 5 que no existen, o se rechazaba un traslado que si se podia
-- atender a medias.
--
-- CON ESTA COLUMNA SON TRES CIFRAS Y NO DOS, y la distincion es el nucleo del
-- modulo:
--
--   solicitada - despachada   lo que el ORIGEN no pudo mandar. No es una
--                             perdida: esa mercancia nunca salio y sigue en el
--                             estante de alguien.
--   despachada - recibida     lo que SE PERDIO EN EL CAMINO. Eso si es una baja
--                             neta de la red, y es lo que se le reclama a la
--                             transportadora.
--
-- Sumarlas en una sola cifra -como pasaba antes- convertia un faltante de
-- bodega en una merma de transporte, que es a quien no le corresponde.
--
-- -----------------------------------------------------------------------------
-- 2. `fecha_despacho` y `fecha_recepcion`: cuando salio y cuando llego
--
-- Existian en el libro mayor -son la fecha de los movimientos- pero no en el
-- traslado, asi que cualquier informe de cumplimiento tenia que deducirlas
-- cruzando `movimientos_inventario`. Aqui se guardan donde se consultan.
--
-- `fecha_recepcion` contra `fecha_estimada_llegada` es lo que dice si la
-- transportadora cumplio; `fecha_recepcion` menos `fecha_despacho` es el
-- transito real.
--
-- -----------------------------------------------------------------------------
-- 3. Tratamiento y cierre de la novedad
--
-- Una novedad era un apunte y nada mas: se escribia y el traslado se daba por
-- cerrado. Pero un faltante no siempre termina ahi. Caben tres desenlaces:
--
--   Reenvio      el origen vuelve a mandar lo que falto
--   Reclamacion  se le cobra a la transportadora
--   Asumido      se da por perdido: ESO es la merma
--
-- Los dos primeros dejan trabajo pendiente, asi que el traslado NO puede
-- cerrarse: sigue "por recibir" hasta que ese desenlace se resuelva. El tercero
-- si lo cierra, porque ya no queda nada que esperar.
--
-- POR QUE `estado` EN LA NOVEDAD Y NO SOLO EN EL TRASLADO. Un traslado puede
-- acumular varias novedades -llego corto Y una lata rota- y cada una se
-- resuelve por su lado. El traslado cierra cuando no le queda ninguna abierta.
--
-- `motivo_cierre` es obligatorio al cerrar, y se guarda: es el porque, y es lo
-- unico que queda para revisar dentro de seis meses por que aquel faltante no
-- se le cobro a nadie.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- transferencias.cantidad_despachada
-- -----------------------------------------------------------------------------
SET @existe := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'transferencias'
    AND COLUMN_NAME  = 'cantidad_despachada'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `transferencias`
     ADD COLUMN `cantidad_despachada` DECIMAL(14,4) NULL
       COMMENT ''Lo que de verdad salio del origen. Nulo hasta el despacho.''
       AFTER `cantidad_solicitada`',
  'SELECT ''cantidad_despachada ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- transferencias.fecha_despacho
-- -----------------------------------------------------------------------------
SET @existe := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'transferencias'
    AND COLUMN_NAME  = 'fecha_despacho'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `transferencias`
     ADD COLUMN `fecha_despacho` DATETIME NULL
       COMMENT ''Cuando salio del origen. Nulo mientras esta Solicitada.''
       AFTER `fecha_solicitud`',
  'SELECT ''fecha_despacho ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- transferencias.fecha_recepcion
-- -----------------------------------------------------------------------------
SET @existe := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'transferencias'
    AND COLUMN_NAME  = 'fecha_recepcion'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `transferencias`
     ADD COLUMN `fecha_recepcion` DATETIME NULL
       COMMENT ''Cuando la conto el destino. Contra fecha_estimada_llegada dice si llego tarde.''
       AFTER `fecha_estimada_llegada`',
  'SELECT ''fecha_recepcion ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- RELLENO DE LO YA EXISTENTE
--
-- Los traslados que ya salieron lo hicieron por la cantidad solicitada, porque
-- hasta hoy no habia otra opcion. Dejarlo en NULL haria que el informe contara
-- como "sin despachar" traslados que se entregaron.
-- -----------------------------------------------------------------------------
UPDATE `transferencias`
   SET `cantidad_despachada` = `cantidad_solicitada`
 WHERE `cantidad_despachada` IS NULL
   AND `estado` IN ('EnTransito','Completada','RecibidaParcial','Cerrada');

-- Las fechas salen del libro mayor, que es donde han estado siempre. El
-- despacho es el Retiro del origen; la recepcion, el Ingreso del destino.
UPDATE `transferencias` t
  JOIN (
        SELECT `transferencia_id`, MIN(`fecha`) AS `f`
          FROM `movimientos_inventario`
         WHERE `transferencia_id` IS NOT NULL
           AND `tipo`   = 'Retiro'
           AND `motivo` = 'Transferencia'
         GROUP BY `transferencia_id`
       ) m ON m.`transferencia_id` = t.`id`
   SET t.`fecha_despacho` = m.`f`
 WHERE t.`fecha_despacho` IS NULL;

UPDATE `transferencias` t
  JOIN (
        SELECT `transferencia_id`, MAX(`fecha`) AS `f`
          FROM `movimientos_inventario`
         WHERE `transferencia_id` IS NOT NULL
           AND `tipo`   = 'Ingreso'
           AND `motivo` = 'Transferencia'
         GROUP BY `transferencia_id`
       ) m ON m.`transferencia_id` = t.`id`
   SET t.`fecha_recepcion` = m.`f`
 WHERE t.`fecha_recepcion` IS NULL;

-- -----------------------------------------------------------------------------
-- CHECK: no se despacha cero, y no se despacha mas de lo que se pidio
--
-- Lo segundo no es una suposicion tecnica sino la regla del negocio: el ajuste
-- del origen existe para mandar MENOS. Mandar de mas seria stock que el destino
-- no pidio y que su bodega no espera.
-- -----------------------------------------------------------------------------
SET @existe := (
  SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = DATABASE()
    AND TABLE_NAME        = 'transferencias'
    AND CONSTRAINT_NAME   = 'chk_transf_despachada'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `transferencias`
     ADD CONSTRAINT `chk_transf_despachada` CHECK (
       `cantidad_despachada` IS NULL
       OR (`cantidad_despachada` > 0
           AND (`cantidad_solicitada` IS NULL
                OR `cantidad_despachada` <= `cantidad_solicitada`)))',
  'SELECT ''chk_transf_despachada ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -----------------------------------------------------------------------------
-- novedades_transferencia: tratamiento, estado y cierre
-- -----------------------------------------------------------------------------
SET @existe := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'novedades_transferencia'
    AND COLUMN_NAME  = 'tratamiento'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `novedades_transferencia`
     ADD COLUMN `tratamiento` ENUM(''Ninguno'',''Reenvio'',''Reclamacion'',''Asumido'')
       NOT NULL DEFAULT ''Ninguno''
       COMMENT ''Que se va a hacer con lo reportado. Reenvio y Reclamacion dejan el traslado pendiente.''
       AFTER `cantidad_afectada`,
     ADD COLUMN `estado` ENUM(''Abierta'',''Cerrada'')
       NOT NULL DEFAULT ''Abierta''
       COMMENT ''Abierta mientras el tratamiento no se resuelve.''
       AFTER `tratamiento`,
     ADD COLUMN `motivo_cierre` TEXT NULL
       COMMENT ''El porque del cierre. Obligatorio al cerrar; es lo que queda para revisar despues.''
       AFTER `estado`,
     ADD COLUMN `fecha_cierre` DATETIME NULL AFTER `motivo_cierre`,
     ADD COLUMN `usuario_cierre_id` INT NULL AFTER `fecha_cierre`',
  'SELECT ''tratamiento ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Las novedades que ya estaban son apuntes historicos sin tratamiento
-- pendiente. Dejarlas 'Abierta' -que es el valor por defecto de la columna, y
-- el correcto para las nuevas- haria aparecer como pendientes traslados que
-- nadie esta esperando.
UPDATE `novedades_transferencia`
   SET `estado` = 'Cerrada'
 WHERE `tratamiento` = 'Ninguno'
   AND `estado` = 'Abierta';

-- Una novedad solo puede seguir abierta si hay algo que esperar. Sin este
-- CHECK cabria una 'Abierta' sin tratamiento, que es un pendiente que nadie
-- sabria como cerrar.
SET @existe := (
  SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = DATABASE()
    AND TABLE_NAME        = 'novedades_transferencia'
    AND CONSTRAINT_NAME   = 'chk_novtransf_abierta_con_tratamiento'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `novedades_transferencia`
     ADD CONSTRAINT `chk_novtransf_abierta_con_tratamiento` CHECK (
       `estado` = ''Cerrada'' OR `tratamiento` IN (''Reenvio'',''Reclamacion''))',
  'SELECT ''chk_novtransf_abierta_con_tratamiento ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- FK del responsable del cierre. RESTRICT como el resto de responsables del
-- sistema: quien cerro una novedad no se puede borrar sin perder el rastro.
SET @existe := (
  SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = DATABASE()
    AND TABLE_NAME        = 'novedades_transferencia'
    AND CONSTRAINT_NAME   = 'fk_novtransf_usuario_cierre'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `novedades_transferencia`
     ADD CONSTRAINT `fk_novtransf_usuario_cierre`
     FOREIGN KEY (`usuario_cierre_id`) REFERENCES `usuarios` (`id`)
     ON DELETE RESTRICT ON UPDATE CASCADE',
  'SELECT ''fk_novtransf_usuario_cierre ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Indice por estado: la pantalla pregunta "que novedades siguen abiertas" en
-- cada carga de la lista de traslados.
SET @existe := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'novedades_transferencia'
    AND INDEX_NAME   = 'idx_novtransf_estado'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `novedades_transferencia`
     ADD INDEX `idx_novtransf_estado` (`estado`)',
  'SELECT ''idx_novtransf_estado ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
