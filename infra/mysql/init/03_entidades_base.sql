-- =============================================================================
-- Colorsin - Inventario
-- DDL 02: Entidades base (primeras tablas con claves foráneas)
--
-- Depende de 02_tablas_independientes.sql:
--   productos.unidad_base_id -> unidades_medida.id
--   usuarios.sucursal_id     -> sucursales.id
--
-- Idempotente: se puede correr en MySQL Workbench sobre la base existente o
-- dejar que se aplique solo al recrear el volumen.
-- =============================================================================

USE `colorsin_inventario`;

-- -----------------------------------------------------------------------------
-- 6. PRODUCTOS
--    `unidad_base_id` es la unidad en la que se lleva el stock del producto
--    (ej. pintura en litros, masilla en kilogramos). Las conversiones se hacen
--    contra `unidades_medida.factor_conversion_litros`.
--
--    ON DELETE RESTRICT: no se puede borrar una unidad de medida que algún
--    producto esté usando. Protege el inventario de quedar sin referencia.
--    ON UPDATE CASCADE: si el id de la unidad cambiara, se propaga solo.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `productos` (
  `id`             INT          NOT NULL AUTO_INCREMENT,
  `nombre`         VARCHAR(100) NOT NULL,
  `categoria`      VARCHAR(50)      NULL,
  `descripcion`    TEXT             NULL,
  `unidad_base_id` INT              NULL,
  PRIMARY KEY (`id`),
  KEY `idx_productos_unidad_base` (`unidad_base_id`),
  CONSTRAINT `fk_productos_unidad_base`
    FOREIGN KEY (`unidad_base_id`)
    REFERENCES `unidades_medida` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Catalogo de productos: pinturas, disolventes, insumos';


-- -----------------------------------------------------------------------------
-- 7. USUARIOS
--    `password_hash` guarda SOLO el hash (BCrypt/Argon2), nunca la clave en
--    texto plano. VARCHAR(255) cubre cualquier algoritmo moderno.
--
--    `sucursal_id` es NULL a proposito: el Administrador General no pertenece a
--    una sede concreta. Gerente de Sucursal y Operador si deberian tener una.
--
--    ON DELETE SET NULL: si se elimina una sucursal, sus usuarios NO se borran;
--    quedan sin sede asignada para que un administrador los reubique.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `usuarios` (
  `id`            INT           NOT NULL AUTO_INCREMENT,
  `nombre`        VARCHAR(100)  NOT NULL,
  `email`         VARCHAR(100)  NOT NULL,
  `password_hash` VARCHAR(255)  NOT NULL,
  `rol`           ENUM('Administrador General', 'Gerente de Sucursal', 'Operador') NOT NULL,
  `sucursal_id`   INT               NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_usuarios_email` (`email`),
  KEY `idx_usuarios_sucursal` (`sucursal_id`),
  CONSTRAINT `fk_usuarios_sucursal`
    FOREIGN KEY (`sucursal_id`)
    REFERENCES `sucursales` (`id`)
    ON DELETE SET NULL
    ON UPDATE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Usuarios del sistema con su rol y sede asignada';
