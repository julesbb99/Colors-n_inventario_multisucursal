-- =============================================================================
-- Colorsin - Inventario
-- DDL 03: Control de stock, catalogo de proveedores por producto y lotes
--
-- Depende de 02_tablas_independientes.sql y 03_entidades_base.sql:
--   inventario_sucursal -> sucursales, productos
--   producto_proveedor  -> productos, proveedores
--   lotes               -> productos, sucursales
--
-- Idempotente: se puede correr en MySQL Workbench sobre la base existente o
-- dejar que se aplique solo al recrear el volumen.
-- =============================================================================

USE `colorsin_inventario`;

-- -----------------------------------------------------------------------------
-- 8. INVENTARIO_SUCURSAL
--    Stock consolidado de cada producto en cada sede. Una fila por pareja
--    (sucursal, producto): de ahi la restriccion UNIQUE, que es lo que impide
--    que un mismo producto termine con dos saldos distintos en la misma sede.
--
--    Todas las cantidades van en la unidad base del producto
--    (productos.unidad_base_id), para que sumar y comparar tenga sentido.
--
--    ON DELETE RESTRICT en ambas FK: un producto o una sede con saldo no se
--    borran. En inventario los productos se desactivan, no se eliminan.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `inventario_sucursal` (
  `id`             INT           NOT NULL AUTO_INCREMENT,
  `sucursal_id`    INT           NOT NULL,
  `producto_id`    INT           NOT NULL,
  `cantidad_base`  DECIMAL(14,4) NOT NULL DEFAULT 0,
  `stock_minimo`   DECIMAL(14,4) NOT NULL DEFAULT 0,
  `costo_promedio` DECIMAL(14,4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_inventario_sucursal_producto` (`sucursal_id`, `producto_id`),
  KEY `idx_inventario_producto` (`producto_id`),
  CONSTRAINT `fk_inventario_sucursal`
    FOREIGN KEY (`sucursal_id`)
    REFERENCES `sucursales` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_inventario_producto`
    FOREIGN KEY (`producto_id`)
    REFERENCES `productos` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Saldo de cada producto en cada sede, en unidad base';


-- -----------------------------------------------------------------------------
-- 9. PRODUCTO_PROVEEDOR
--    Tabla puente: que proveedores surten cada producto y a que precio de
--    referencia. Un producto puede tener varios proveedores y un proveedor
--    varios productos, pero la pareja no se repite (UNIQUE).
--
--    `precio_referencia` es orientativo para cotizar; el costo real de cada
--    compra se registra en el movimiento correspondiente.
--
--    ON DELETE CASCADE: es una tabla de asociacion pura. Si desaparece el
--    producto o el proveedor, el vinculo entre ambos deja de existir.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `producto_proveedor` (
  `id`                INT           NOT NULL AUTO_INCREMENT,
  `producto_id`       INT           NOT NULL,
  `proveedor_id`      INT           NOT NULL,
  `precio_referencia` DECIMAL(12,2)     NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_producto_proveedor` (`producto_id`, `proveedor_id`),
  KEY `idx_prodprov_proveedor` (`proveedor_id`),
  CONSTRAINT `fk_prodprov_producto`
    FOREIGN KEY (`producto_id`)
    REFERENCES `productos` (`id`)
    ON DELETE CASCADE
    ON UPDATE CASCADE,
  CONSTRAINT `fk_prodprov_proveedor`
    FOREIGN KEY (`proveedor_id`)
    REFERENCES `proveedores` (`id`)
    ON DELETE CASCADE
    ON UPDATE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Proveedores que surten cada producto y su precio de referencia';


-- -----------------------------------------------------------------------------
-- 10. LOTES
--     Trazabilidad: de que lote proviene el producto que hay en cada sede y
--     cuando vence. Clave para pinturas y disolventes, que caducan.
--
--     `idx_lotes_vencimiento` esta pensado para consultas FEFO
--     (First Expired, First Out) y alertas de proximos a vencer.
--
--     ON DELETE RESTRICT: los lotes son registro historico y de trazabilidad;
--     no deben desaparecer al borrar un producto o una sede.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `lotes` (
  `id`                INT           NOT NULL AUTO_INCREMENT,
  `producto_id`       INT           NOT NULL,
  `sucursal_id`       INT           NOT NULL,
  `numero_lote`       VARCHAR(50)   NOT NULL,
  `fecha_vencimiento` DATE              NULL,
  `cantidad_base`     DECIMAL(14,4)     NULL,
  `fecha_ingreso`     DATETIME          NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_lotes_producto_sucursal` (`producto_id`, `sucursal_id`),
  KEY `idx_lotes_sucursal` (`sucursal_id`),
  KEY `idx_lotes_vencimiento` (`fecha_vencimiento`),
  KEY `idx_lotes_numero` (`numero_lote`),
  CONSTRAINT `fk_lotes_producto`
    FOREIGN KEY (`producto_id`)
    REFERENCES `productos` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_lotes_sucursal`
    FOREIGN KEY (`sucursal_id`)
    REFERENCES `sucursales` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Lotes por producto y sede, con vencimiento para control FEFO';
