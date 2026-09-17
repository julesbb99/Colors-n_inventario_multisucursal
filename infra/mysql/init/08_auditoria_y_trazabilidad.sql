-- =============================================================================
-- MODULO COMUN: bitacora de auditoria
-- MODULO INVENTARIO: trazabilidad por lote en el libro mayor
--
-- Equivale a la migracion de EF Core `20260917144414_AuditoriaYTrazabilidadLote`.
-- Las dos definiciones deben moverse juntas: si cambias una, cambia la otra, o
-- el esquema que levanta Docker dejara de coincidir con el que espera la API.
--
-- Va numerado 08, despues del seed (07), aunque sea DDL. No importa el orden:
-- no depende de ningun dato, y renumerar los scripts existentes obligaria a
-- recrear el volumen de todos modos.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. AUDITORIA_EVENTOS
--
-- Bitacora transversal de cambios criticos. No sustituye a
-- `movimientos_inventario`: aquel responde "cuanto habia", este responde "quien
-- hizo que", incluso para acciones que no mueven cantidades.
--
-- `id` es BIGINT y no INT: esta tabla crece con cada operacion del sistema, no
-- con cada producto, y los 2.147 millones de un INT se alcanzan.
--
-- SOBRE LA INMUTABILIDAD: hoy la garantiza solo la aplicacion, que no expone
-- forma de editar ni borrar eventos. Para que la base la imponga harian falta
-- dos triggers:
--
--   CREATE TRIGGER `trg_auditoria_eventos_no_update`
--   BEFORE UPDATE ON `auditoria_eventos` FOR EACH ROW
--   SIGNAL SQLSTATE '45000'
--   SET MESSAGE_TEXT = 'auditoria_eventos es inmutable: no se permite UPDATE';
--   -- y su equivalente BEFORE DELETE
--
-- No estan puestos porque crearlos falla con ERROR 1419: con el log binario
-- activo MySQL exige SUPER, y el usuario `colorsin` no lo tiene (SET_USER_ID
-- tampoco alcanza; esta probado). Para habilitarlos hay que agregar
--   --log-bin-trust-function-creators=1
-- al `command:` del servicio mysql en docker-compose.yml y reiniciar el
-- contenedor.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `auditoria_eventos` (
  `id`         BIGINT        NOT NULL AUTO_INCREMENT,
  `modulo`     VARCHAR(50)   NOT NULL,
  `accion`     VARCHAR(80)   NOT NULL,
  `usuario_id` INT           NOT NULL,
  `detalle`    VARCHAR(1000)     NULL,
  `fecha`      DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  -- Consulta tipica: que paso en tal modulo entre tales fechas.
  KEY `idx_auditoria_modulo_fecha` (`modulo`, `fecha`),
  KEY `idx_auditoria_usuario` (`usuario_id`),
  -- RESTRICT: borrar un usuario no puede llevarse por delante el rastro de lo
  -- que hizo. Es el mismo criterio del resto de la auditoria.
  CONSTRAINT `fk_auditoria_usuario`
    FOREIGN KEY (`usuario_id`)
    REFERENCES `usuarios` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Bitacora de solo-anexar: quien cambio que y cuando';


-- -----------------------------------------------------------------------------
-- 2. MOVIMIENTOS_INVENTARIO: lote y observaciones
--
-- `lote_id` es lo que cierra la trazabilidad FEFO: sin el, el libro mayor sabe
-- cuanto salio pero no de que lote, y el saldo por lote no se puede cuadrar
-- contra el consolidado.
--
-- Nulo a proposito: no todo movimiento se imputa a un lote (un ajuste global,
-- por ejemplo).
--
-- MySQL 8.4 no admite ADD COLUMN IF NOT EXISTS, asi que la idempotencia se
-- resuelve consultando information_schema y armando la sentencia solo si hace
-- falta.
-- -----------------------------------------------------------------------------
SET @existe_lote_id := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'movimientos_inventario'
    AND COLUMN_NAME  = 'lote_id');

SET @sql := IF(@existe_lote_id = 0,
  'ALTER TABLE `movimientos_inventario`
     ADD COLUMN `lote_id` INT NULL AFTER `cantidad_base`,
     ADD KEY `idx_movinv_lote` (`lote_id`),
     ADD CONSTRAINT `fk_movinv_lote`
       FOREIGN KEY (`lote_id`) REFERENCES `lotes` (`id`)
       ON DELETE RESTRICT ON UPDATE CASCADE',
  'SELECT ''lote_id ya existe'' AS aviso');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @existe_obs := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'movimientos_inventario'
    AND COLUMN_NAME  = 'observaciones');

SET @sql := IF(@existe_obs = 0,
  'ALTER TABLE `movimientos_inventario`
     ADD COLUMN `observaciones` VARCHAR(255) NULL AFTER `lote_id`',
  'SELECT ''observaciones ya existe'' AS aviso');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
