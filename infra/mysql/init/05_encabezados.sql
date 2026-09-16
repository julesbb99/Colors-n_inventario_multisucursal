-- =============================================================================
-- Colorsin - Inventario
-- DDL 04: Encabezados de compras, ventas y transferencias entre sedes
--
-- Son los documentos "cabecera". El detalle linea por linea (que producto, que
-- cantidad, a que precio) va en tablas aparte que dependen de estas.
--
-- Depende de todos los scripts anteriores:
--   ordenes_compra -> proveedores, sucursales
--   ventas         -> clientes, sucursales, usuarios
--   transferencias -> productos, sucursales (x2), transportadoras, unidades_medida
--
-- Idempotente: se puede correr en MySQL Workbench sobre la base existente o
-- dejar que se aplique solo al recrear el volumen.
-- =============================================================================

USE `colorsin_inventario`;

-- -----------------------------------------------------------------------------
-- 11. ORDENES_COMPRA
--     Pedido a un proveedor para reabastecer una sede. El ciclo de vida va por
--     `estado`; solo al pasar a 'Recibida' se afecta el stock real.
--
--     `plazo_pago_dias` son los dias de credito pactados (0 = contado).
--
--     ON DELETE RESTRICT: una orden es un documento comercial. No se borra el
--     proveedor ni la sede mientras existan ordenes que los referencien.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `ordenes_compra` (
  `id`              INT      NOT NULL AUTO_INCREMENT,
  `proveedor_id`    INT      NOT NULL,
  `sucursal_id`     INT      NOT NULL,
  `fecha`           DATETIME     NULL DEFAULT CURRENT_TIMESTAMP,
  `estado`          ENUM('Pendiente', 'Confirmada', 'Recibida', 'Cancelada')
                                 NULL DEFAULT 'Pendiente',
  `plazo_pago_dias` INT          NULL,
  PRIMARY KEY (`id`),
  KEY `idx_oc_proveedor` (`proveedor_id`),
  KEY `idx_oc_sucursal_fecha` (`sucursal_id`, `fecha`),
  KEY `idx_oc_estado` (`estado`),
  CONSTRAINT `fk_oc_proveedor`
    FOREIGN KEY (`proveedor_id`)
    REFERENCES `proveedores` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_oc_sucursal`
    FOREIGN KEY (`sucursal_id`)
    REFERENCES `sucursales` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  -- Un plazo de pago negativo no tiene sentido.
  CONSTRAINT `chk_oc_plazo_pago`
    CHECK (`plazo_pago_dias` IS NULL OR `plazo_pago_dias` >= 0)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Encabezado de ordenes de compra a proveedores';


-- -----------------------------------------------------------------------------
-- 12. VENTAS
--     Encabezado de la venta. `usuario_id` deja constancia de quien la registro:
--     es el rastro de auditoria de la operacion.
--
--     `total` se calcula sumando el detalle; queda NULL mientras la venta se
--     esta armando y se consolida al cerrarla.
--
--     ON DELETE RESTRICT tambien en `usuario_id`: si un empleado se va, se
--     desactiva su cuenta, no se borra. Borrarlo dejaria ventas sin responsable.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `ventas` (
  `id`          INT           NOT NULL AUTO_INCREMENT,
  `cliente_id`  INT           NOT NULL,
  `sucursal_id` INT           NOT NULL,
  `usuario_id`  INT           NOT NULL,
  `fecha`       DATETIME          NULL DEFAULT CURRENT_TIMESTAMP,
  `total`       DECIMAL(14,2)     NULL,
  PRIMARY KEY (`id`),
  KEY `idx_ventas_cliente` (`cliente_id`),
  KEY `idx_ventas_usuario` (`usuario_id`),
  KEY `idx_ventas_sucursal_fecha` (`sucursal_id`, `fecha`),
  KEY `idx_ventas_fecha` (`fecha`),
  CONSTRAINT `fk_ventas_cliente`
    FOREIGN KEY (`cliente_id`)
    REFERENCES `clientes` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_ventas_sucursal`
    FOREIGN KEY (`sucursal_id`)
    REFERENCES `sucursales` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_ventas_usuario`
    FOREIGN KEY (`usuario_id`)
    REFERENCES `usuarios` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  -- Un total negativo indicaria una devolucion, que va por otro documento.
  CONSTRAINT `chk_ventas_total`
    CHECK (`total` IS NULL OR `total` >= 0)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Encabezado de ventas a clientes';


-- -----------------------------------------------------------------------------
-- 13. TRANSFERENCIAS
--     Traslado de un producto entre dos sedes de la red.
--
--     Dos FK apuntan a la MISMA tabla (`sucursales`), una por origen y otra por
--     destino: por eso cada restriccion necesita nombre propio.
--
--     `unidad_id` puede ser distinta de la unidad base del producto: se puede
--     pedir en galones aunque el stock se lleve en litros. La conversion se hace
--     con `unidades_medida.factor_conversion_litros`.
--
--     `cantidad_recibida` es NULL hasta que la sede destino confirma. Comparada
--     con `cantidad_solicitada` es lo que distingue 'RecibidaCompleta' de
--     'RecibidaParcial'.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `transferencias` (
  `id`                     INT           NOT NULL AUTO_INCREMENT,
  `producto_id`            INT           NOT NULL,
  `sucursal_origen_id`     INT           NOT NULL,
  `sucursal_destino_id`    INT           NOT NULL,
  `transportadora_id`      INT           NOT NULL,
  `cantidad_solicitada`    DECIMAL(14,4)     NULL,
  `cantidad_recibida`      DECIMAL(14,4)     NULL,
  `unidad_id`              INT           NOT NULL,
  `estado`                 ENUM('Solicitada', 'EnPreparacion', 'EnTransito',
                                'RecibidaCompleta', 'RecibidaParcial')
                                             NULL DEFAULT 'Solicitada',
  `urgencia`               ENUM('Baja', 'Media', 'Alta')
                                             NULL DEFAULT 'Media',
  `fecha_solicitud`        DATETIME          NULL DEFAULT CURRENT_TIMESTAMP,
  `fecha_estimada_llegada` DATETIME          NULL,
  PRIMARY KEY (`id`),
  KEY `idx_transf_producto` (`producto_id`),
  KEY `idx_transf_origen` (`sucursal_origen_id`),
  KEY `idx_transf_destino_estado` (`sucursal_destino_id`, `estado`),
  KEY `idx_transf_transportadora` (`transportadora_id`),
  KEY `idx_transf_unidad` (`unidad_id`),
  KEY `idx_transf_estado` (`estado`),
  CONSTRAINT `fk_transf_producto`
    FOREIGN KEY (`producto_id`)
    REFERENCES `productos` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  -- Estas dos FK van con ON UPDATE RESTRICT y no CASCADE a proposito:
  -- MySQL prohibe (error 3823) que una columna con accion referencial
  -- participe en un CHECK, y aqui el CHECK de sedes distintas vale mas.
  -- CASCADE no se pierde nada: `sucursales.id` es AUTO_INCREMENT, nunca cambia.
  CONSTRAINT `fk_transf_sucursal_origen`
    FOREIGN KEY (`sucursal_origen_id`)
    REFERENCES `sucursales` (`id`)
    ON DELETE RESTRICT
    ON UPDATE RESTRICT,
  CONSTRAINT `fk_transf_sucursal_destino`
    FOREIGN KEY (`sucursal_destino_id`)
    REFERENCES `sucursales` (`id`)
    ON DELETE RESTRICT
    ON UPDATE RESTRICT,
  CONSTRAINT `fk_transf_transportadora`
    FOREIGN KEY (`transportadora_id`)
    REFERENCES `transportadoras` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_transf_unidad`
    FOREIGN KEY (`unidad_id`)
    REFERENCES `unidades_medida` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  -- Una sede no se transfiere mercancia a si misma.
  CONSTRAINT `chk_transf_sedes_distintas`
    CHECK (`sucursal_origen_id` <> `sucursal_destino_id`),
  -- Cantidades coherentes: se pide mas que cero, se recibe cero o mas.
  CONSTRAINT `chk_transf_cantidades`
    CHECK (
      (`cantidad_solicitada` IS NULL OR `cantidad_solicitada` > 0)
      AND (`cantidad_recibida` IS NULL OR `cantidad_recibida` >= 0)
    )
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Traslados de producto entre sedes de la red Colorsin';
