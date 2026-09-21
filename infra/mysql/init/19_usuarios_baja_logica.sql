-- =============================================================================
-- USUARIOS: baja logica
--
-- Anade `usuarios.activo`. Un perfil deshabilitado NO PUEDE INICIAR SESION,
-- pero sigue existiendo con toda su historia detras.
--
-- POR QUE BAJA LOGICA Y NO BORRADO. Un usuario no se puede borrar aunque se
-- quisiera: `ventas.usuario_id`, `movimientos_inventario.usuario_id`,
-- `transferencias.usuario_id`, `ordenes_compra.usuario_id`,
-- `novedades_transferencia.usuario_id` y `auditoria_eventos.usuario_id` lo
-- referencian con ON DELETE RESTRICT, y eso es deliberado: la bitacora no puede
-- quedarse sin responsable. Un DELETE fallaria contra la primera clave foranea,
-- y forzarlo dejaria ventas firmadas por nadie.
--
-- Lo que se quiere de verdad al "eliminar" a alguien es que deje de entrar, y
-- eso es justo lo que hace esta columna.
--
-- QUIEN PUEDE DESHABILITAR A QUIEN vive en el codigo
-- (ReglasGestionUsuario), no aqui: es una regla de negocio y la base no conoce
-- quien esta haciendo la peticion. En resumen:
--
--   Administrador General  deshabilita gerentes y operadores, en cualquier sede
--   Gerente de Sucursal    deshabilita operadores, SOLO de su sede
--   Operador               a nadie
--
-- Y dos reglas que no son de jerarquia sino de sentido comun, tambien en el
-- codigo: nadie se deshabilita a si mismo -te dejaria fuera en el acto- y
-- ningun Administrador General se deshabilita desde la aplicacion, por lo mismo
-- que ninguno se crea desde ella.
--
-- DEFAULT 1: todo el que ya existe sigue entrando. Una columna nueva que
-- deshabilitara a todo el mundo dejaria el sistema sin nadie dentro.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

SET @existe := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'usuarios'
    AND COLUMN_NAME  = 'activo'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `usuarios`
     ADD COLUMN `activo` TINYINT(1) NOT NULL DEFAULT 1
       COMMENT ''0 = perfil deshabilitado: no puede iniciar sesion, pero conserva su historia.''
       AFTER `sucursal_id`',
  'SELECT ''usuarios.activo ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- El listado del modulo de usuarios filtra por sede y por estado en la misma
-- consulta.
SET @existe := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'usuarios'
    AND INDEX_NAME   = 'idx_usuarios_activo'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `usuarios` ADD INDEX `idx_usuarios_activo` (`activo`)',
  'SELECT ''idx_usuarios_activo ya existe'' AS aviso');

PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- SEGURO CONTRA QUEDARSE SIN ADMINISTRADOR.
--
-- La aplicacion no deja deshabilitar a un Administrador General, pero esta
-- tabla se toca tambien desde Workbench. Un UPDATE a mano que los apague a
-- todos dejaria el sistema sin nadie que pueda reactivar a nadie, y eso solo se
-- arregla volviendo a la base. El trigger lo impide en el unico sitio donde no
-- se puede esquivar.
DROP TRIGGER IF EXISTS `trg_usuarios_ultimo_admin`;

DELIMITER $$

CREATE TRIGGER `trg_usuarios_ultimo_admin`
BEFORE UPDATE ON `usuarios`
FOR EACH ROW
BEGIN
  IF OLD.`activo` = 1
     AND NEW.`activo` = 0
     AND OLD.`rol` = 'Administrador General'
     AND (SELECT COUNT(*) FROM `usuarios`
           WHERE `rol` = 'Administrador General'
             AND `activo` = 1
             AND `id` <> OLD.`id`) = 0
  THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'No se puede deshabilitar al ultimo Administrador General activo.';
  END IF;
END$$

DELIMITER ;
