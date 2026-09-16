-- =============================================================================
-- Colorsin - Inventario
-- DDL 01: Tablas independientes (sin claves foráneas hacia otras tablas)
--
-- Estas son las tablas "maestras" o de catálogo. Se crean primero porque el
-- resto del modelo (productos, movimientos, despachos) apuntará hacia ellas.
--
-- Es idempotente (CREATE TABLE IF NOT EXISTS), así que se puede correr:
--   a) Automáticamente, al crear el volumen por primera vez.
--   b) A mano en MySQL Workbench sobre una base que ya existe.
-- =============================================================================

USE `colorsin_inventario`;

-- -----------------------------------------------------------------------------
-- 1. UNIDADES_MEDIDA
--    Catálogo de unidades (litro, galón, kilogramo...). `factor_conversion_litros`
--    permite normalizar todo a litros para reportes; es NULL en unidades que no
--    son de volumen (ej. kilogramos, unidades sueltas).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `unidades_medida` (
  `id`                       INT           NOT NULL AUTO_INCREMENT,
  `nombre`                   VARCHAR(50)   NOT NULL,
  `simbolo`                  VARCHAR(10)   NOT NULL,
  `factor_conversion_litros` DECIMAL(12,6)     NULL,
  PRIMARY KEY (`id`)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Catalogo de unidades de medida y su equivalencia en litros';


-- -----------------------------------------------------------------------------
-- 2. PROVEEDORES
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `proveedores` (
  `id`       INT          NOT NULL AUTO_INCREMENT,
  `nombre`   VARCHAR(100) NOT NULL,
  `contacto` VARCHAR(100)     NULL,
  `telefono` VARCHAR(20)  NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Empresas que suministran materia prima y producto terminado';


-- -----------------------------------------------------------------------------
-- 3. SUCURSALES
--    `rol_red` distingue la sede Matriz de las demas sucursales de la red.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `sucursales` (
  `id`          INT                          NOT NULL AUTO_INCREMENT,
  `nombre`      VARCHAR(100)                 NOT NULL,
  `ciudad`      VARCHAR(80)                  NOT NULL,
  `direccion`   VARCHAR(150)                     NULL,
  `rol_red`     ENUM('Matriz', 'Sucursal')   NOT NULL,
  `descripcion` VARCHAR(200)                     NULL,
  PRIMARY KEY (`id`)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Sedes fisicas de Colorsin (matriz y sucursales)';


-- -----------------------------------------------------------------------------
-- 4. TRANSPORTADORAS
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `transportadoras` (
  `id`            INT                          NOT NULL AUTO_INCREMENT,
  `nombre`        VARCHAR(100)                 NOT NULL,
  `tipo_servicio` ENUM('urgente', 'estandar')  NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Empresas de transporte usadas para despachos entre sedes y a clientes';


-- -----------------------------------------------------------------------------
-- 5. CLIENTES
--    `documento` es UNIQUE: NIT o cedula, segun `tipo_persona`.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `clientes` (
  `id`           INT                          NOT NULL AUTO_INCREMENT,
  `razon_social` VARCHAR(150)                 NOT NULL,
  `tipo_persona` ENUM('Natural', 'Juridica')  NOT NULL,
  `documento`    VARCHAR(20)                  NOT NULL,
  `telefono`     VARCHAR(20)                      NULL,
  `email`        VARCHAR(100)                     NULL,
  `direccion`    VARCHAR(150)                     NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_clientes_documento` (`documento`)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Clientes de Colorsin, personas naturales y juridicas';
