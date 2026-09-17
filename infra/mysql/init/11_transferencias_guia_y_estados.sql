-- =============================================================================
-- MODULO TRANSFERENCIAS: guia, transportadora opcional, ciclo de vida nuevo
--                        y usuario responsable
-- MODULO INVENTARIO: vinculo del libro mayor con el traslado que lo origino
--
-- Equivale a las migraciones de EF Core `20260917211412_TransferenciasGuiaYEstados`
-- y `20260917212802_UsuarioResponsableTransferencia`.
-- Las dos definiciones deben moverse juntas: si cambias una, cambia la otra, o
-- el esquema que levanta Docker dejara de coincidir con el que espera la API.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. TRANSFERENCIAS: nuevo ciclo de vida
--
--     Solicitada --despacho--> EnTransito --recepcion--> Completada
--                                         \-recepcion--> RecibidaParcial
--     Solicitada --------------------------------------> Rechazada
--     Solicitada --------------------------------------> Cancelada
--
-- CAMBIOS RESPECTO A LA LISTA ANTERIOR
--   'RecibidaCompleta' paso a llamarse 'Completada'.
--   'EnPreparacion' se retiro: ningun flujo lo usaba.
--   Entran 'Rechazada' y 'Cancelada', que solo salen de 'Solicitada': despues
--   del despacho la mercancia ya salio del origen y anular el traslado dejaria
--   stock sin dueno.
--   'RecibidaParcial' SE CONSERVA. Sin el, una recepcion corta no tendria como
--   representarse: marcarla Completada seria mentir y dejarla EnTransito
--   tambien. La tabla tiene `cantidad_recibida` aparte de
--   `cantidad_solicitada` justamente para ese caso.
--
-- Esta lista debe coincidir valor por valor, y en el mismo orden, con el enum
-- EstadoTransferencia del dominio: un estado que falte aqui falla al guardarse
-- con el error 1265 ("Data truncated for column 'estado'").
--
-- MODIFY es idempotente por naturaleza. Si hubiera filas en 'EnPreparacion' o
-- 'RecibidaCompleta', MySQL las rechazaria; revisar antes con:
--   SELECT estado, COUNT(1) FROM transferencias GROUP BY estado;
-- -----------------------------------------------------------------------------
ALTER TABLE `transferencias`
  MODIFY COLUMN `estado`
    ENUM('Solicitada', 'EnTransito', 'Completada',
         'RecibidaParcial', 'Rechazada', 'Cancelada')
    NULL DEFAULT 'Solicitada';


-- -----------------------------------------------------------------------------
-- 2. TRANSFERENCIAS: la transportadora pasa a ser opcional
--
-- Al SOLICITAR el traslado todavia no se sabe quien lo va a mover: eso se
-- decide al despachar. Con la columna NOT NULL habia que inventarse una
-- transportadora en el momento de pedir el producto.
--
-- La clave foranea `fk_transf_transportadora` sigue vigente y no hay que
-- recrearla: MySQL admite MODIFY sobre una columna referenciada mientras el
-- tipo siga siendo compatible.
-- -----------------------------------------------------------------------------
ALTER TABLE `transferencias`
  MODIFY COLUMN `transportadora_id` INT NULL;


-- -----------------------------------------------------------------------------
-- 3. TRANSFERENCIAS: numero de guia
--
-- Se asigna junto con la transportadora, al despachar, y es con lo que se
-- reclama si la carga llega mal o no llega.
--
-- MySQL 8.4 no admite ADD COLUMN IF NOT EXISTS, asi que la idempotencia se
-- resuelve consultando information_schema y armando la sentencia solo si hace
-- falta.
-- -----------------------------------------------------------------------------
SET @existe_guia := (
  SELECT COUNT(1) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'transferencias'
    AND COLUMN_NAME  = 'guia');

SET @sql := IF(@existe_guia = 0,
  'ALTER TABLE `transferencias`
     ADD COLUMN `guia` VARCHAR(50) NULL AFTER `transportadora_id`',
  'SELECT ''transferencias.guia ya existe'' AS aviso');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- -----------------------------------------------------------------------------
-- 4. MOVIMIENTOS_INVENTARIO: de que traslado viene el movimiento
--
-- No es solo trazabilidad. La RECEPCION de un traslado necesita saber de que
-- lotes salio la mercancia en el origen, para recrearlos en el destino con el
-- mismo numero y vencimiento y no perder la trazabilidad del fabricante. Ese
-- dato solo vive en las filas del libro mayor que genero el despacho, y sin
-- esta columna no hay forma de aislar cuales son: `observaciones` es texto
-- libre y no sirve para consultar.
--
-- Nula en todo lo que no sea un traslado: compras, ventas, ajustes.
-- RESTRICT en la FK, como el resto del libro mayor: borrar un traslado no puede
-- llevarse por delante los movimientos que genero.
-- -----------------------------------------------------------------------------
SET @existe_transf_id := (
  SELECT COUNT(1) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'movimientos_inventario'
    AND COLUMN_NAME  = 'transferencia_id');

SET @sql := IF(@existe_transf_id = 0,
  'ALTER TABLE `movimientos_inventario`
     ADD COLUMN `transferencia_id` INT NULL AFTER `lote_id`,
     ADD KEY `idx_movinv_transferencia` (`transferencia_id`),
     ADD CONSTRAINT `fk_movinv_transferencia`
       FOREIGN KEY (`transferencia_id`) REFERENCES `transferencias` (`id`)
       ON DELETE RESTRICT ON UPDATE CASCADE',
  'SELECT ''movimientos_inventario.transferencia_id ya existe'' AS aviso');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- -----------------------------------------------------------------------------
-- 5. TRANSFERENCIAS: quien solicito el traslado
--
-- Hasta ahora ese dato solo sobrevivia en `auditoria_eventos`, donde no se puede
-- consultar desde el traslado. Es el mismo caso que `ordenes_compra.usuario_id`.
--
-- OJO con no confundir los tres responsables del ciclo: este es quien PIDE el
-- producto. Quien despacha y quien recibe quedan en el `usuario_id` de sus
-- respectivos movimientos de inventario, y los tres en la bitacora.
--
-- RESTRICT en la FK: un usuario con traslados a su nombre no se puede borrar.
--
-- NOT NULL sin DEFAULT a proposito. Poner DEFAULT 0 sugeriria que el cero es un
-- usuario valido, y no lo es. Sobre una tabla con filas, MySQL rellenaria con
-- cero y la FK rechazaria la sentencia: en ese caso hay que decidir primero a
-- que usuario se atribuyen los traslados historicos.
-- -----------------------------------------------------------------------------
SET @existe_usuario_transf := (
  SELECT COUNT(1) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'transferencias'
    AND COLUMN_NAME  = 'usuario_id');

SET @sql := IF(@existe_usuario_transf = 0,
  'ALTER TABLE `transferencias`
     ADD COLUMN `usuario_id` INT NOT NULL AFTER `sucursal_destino_id`,
     ADD KEY `idx_transf_usuario` (`usuario_id`),
     ADD CONSTRAINT `fk_transf_usuario`
       FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`)
       ON DELETE RESTRICT ON UPDATE CASCADE',
  'SELECT ''transferencias.usuario_id ya existe'' AS aviso');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
