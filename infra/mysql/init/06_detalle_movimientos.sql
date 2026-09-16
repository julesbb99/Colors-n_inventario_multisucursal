-- =============================================================================
-- Colorsin - Inventario
-- DDL 05: Detalle de documentos, novedades de transferencia y libro mayor
--
-- Regla de borrado que atraviesa todo este script:
--   * El DETALLE pertenece a su encabezado -> ON DELETE CASCADE.
--     Una linea de compra sin su orden no significa nada.
--   * Los CATALOGOS referenciados (producto, unidad, usuario) -> RESTRICT.
--     No se borra un producto que aparece en documentos historicos.
--   * MOVIMIENTOS_INVENTARIO es auditoria: TODO RESTRICT, sin excepcion.
--     Un libro mayor que se borra en cascada deja de ser auditable.
--
-- Idempotente: se puede correr en MySQL Workbench sobre la base existente o
-- dejar que se aplique solo al recrear el volumen.
-- =============================================================================

USE `colorsin_inventario`;

-- -----------------------------------------------------------------------------
-- 14. ORDEN_COMPRA_DETALLE
--     Lineas de la orden de compra. `descuento` se interpreta como PORCENTAJE
--     (0 a 100), que es lo que sugiere DECIMAL(5,2) con DEFAULT 0.
--
--     `unidad_id` puede diferir de la unidad base del producto: se compra en
--     canecas aunque el stock se lleve en litros.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `orden_compra_detalle` (
  `id`              INT           NOT NULL AUTO_INCREMENT,
  `orden_compra_id` INT           NOT NULL,
  `producto_id`     INT           NOT NULL,
  `cantidad`        DECIMAL(14,4)     NULL,
  `unidad_id`       INT           NOT NULL,
  `precio_unitario` DECIMAL(12,2)     NULL,
  `descuento`       DECIMAL(5,2)  NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `idx_ocd_orden` (`orden_compra_id`),
  KEY `idx_ocd_producto` (`producto_id`),
  KEY `idx_ocd_unidad` (`unidad_id`),
  CONSTRAINT `fk_ocd_orden`
    FOREIGN KEY (`orden_compra_id`)
    REFERENCES `ordenes_compra` (`id`)
    ON DELETE CASCADE
    ON UPDATE CASCADE,
  CONSTRAINT `fk_ocd_producto`
    FOREIGN KEY (`producto_id`)
    REFERENCES `productos` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_ocd_unidad`
    FOREIGN KEY (`unidad_id`)
    REFERENCES `unidades_medida` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `chk_ocd_cantidad`
    CHECK (`cantidad` IS NULL OR `cantidad` > 0),
  CONSTRAINT `chk_ocd_precio`
    CHECK (`precio_unitario` IS NULL OR `precio_unitario` >= 0),
  CONSTRAINT `chk_ocd_descuento`
    CHECK (`descuento` >= 0 AND `descuento` <= 100)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Lineas de detalle de las ordenes de compra';


-- -----------------------------------------------------------------------------
-- 15. VENTA_DETALLE
--     Lineas de la venta. Mismo criterio que la compra.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `venta_detalle` (
  `id`              INT           NOT NULL AUTO_INCREMENT,
  `venta_id`        INT           NOT NULL,
  `producto_id`     INT           NOT NULL,
  `cantidad`        DECIMAL(12,2)     NULL,
  `unidad_id`       INT           NOT NULL,
  `precio_unitario` DECIMAL(12,2)     NULL,
  `descuento`       DECIMAL(5,2)  NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `idx_vd_venta` (`venta_id`),
  KEY `idx_vd_producto` (`producto_id`),
  KEY `idx_vd_unidad` (`unidad_id`),
  CONSTRAINT `fk_vd_venta`
    FOREIGN KEY (`venta_id`)
    REFERENCES `ventas` (`id`)
    ON DELETE CASCADE
    ON UPDATE CASCADE,
  CONSTRAINT `fk_vd_producto`
    FOREIGN KEY (`producto_id`)
    REFERENCES `productos` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_vd_unidad`
    FOREIGN KEY (`unidad_id`)
    REFERENCES `unidades_medida` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `chk_vd_cantidad`
    CHECK (`cantidad` IS NULL OR `cantidad` > 0),
  CONSTRAINT `chk_vd_precio`
    CHECK (`precio_unitario` IS NULL OR `precio_unitario` >= 0),
  CONSTRAINT `chk_vd_descuento`
    CHECK (`descuento` >= 0 AND `descuento` <= 100)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Lineas de detalle de las ventas';


-- -----------------------------------------------------------------------------
-- 16. NOVEDADES_TRANSFERENCIA
--     Incidencias reportadas sobre un traslado: faltantes, averias, sobrantes o
--     retrasos. Varias por transferencia (una averia y un retraso a la vez).
--
--     `usuario_id` es quien reporta: RESTRICT, porque la novedad puede derivar
--     en reclamo a la transportadora y necesita responsable.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `novedades_transferencia` (
  `id`                INT           NOT NULL AUTO_INCREMENT,
  `transferencia_id`  INT           NOT NULL,
  `usuario_id`        INT           NOT NULL,
  `tipo`              ENUM('Faltante', 'Averia', 'Sobrante', 'Retraso') NULL,
  `cantidad_afectada` DECIMAL(14,4)     NULL,
  `observaciones`     TEXT              NULL,
  `fecha`             DATETIME          NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_novtransf_transferencia` (`transferencia_id`),
  KEY `idx_novtransf_usuario` (`usuario_id`),
  KEY `idx_novtransf_tipo` (`tipo`),
  CONSTRAINT `fk_novtransf_transferencia`
    FOREIGN KEY (`transferencia_id`)
    REFERENCES `transferencias` (`id`)
    ON DELETE CASCADE
    ON UPDATE CASCADE,
  CONSTRAINT `fk_novtransf_usuario`
    FOREIGN KEY (`usuario_id`)
    REFERENCES `usuarios` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `chk_novtransf_cantidad`
    CHECK (`cantidad_afectada` IS NULL OR `cantidad_afectada` >= 0)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Incidencias reportadas sobre traslados entre sedes';


-- -----------------------------------------------------------------------------
-- 17. MOVIMIENTOS_INVENTARIO
--     Libro mayor del inventario: cada entrada y salida de stock, una fila.
--     El saldo de `inventario_sucursal` deberia poder reconstruirse sumando
--     esta tabla; si alguna vez no cuadra, aqui esta la evidencia.
--
--     `cantidad` va en la `unidad_id` que uso el operario; `cantidad_base` es
--     esa misma cantidad convertida a la unidad base del producto. Se guardan
--     las dos: una para auditar lo que la persona realmente digito, otra para
--     sumar sin convertir en cada consulta.
--
--     `cantidad` se guarda SIEMPRE positiva: el signo lo da `tipo`
--     ('Ingreso' suma, 'Retiro' resta). De ahi el CHECK > 0.
--
--     TODAS las FK son RESTRICT, incluida la del usuario: esta tabla es el
--     rastro de auditoria y nada debe poder borrarla en cascada.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `movimientos_inventario` (
  `id`            INT           NOT NULL AUTO_INCREMENT,
  `sucursal_id`   INT           NOT NULL,
  `producto_id`   INT           NOT NULL,
  `usuario_id`    INT           NOT NULL,
  `tipo`          ENUM('Ingreso', 'Retiro') NULL,
  `motivo`        ENUM('Compra', 'Venta', 'Ajuste', 'Transferencia',
                       'Merma', 'Devolucion') NULL,
  `cantidad`      DECIMAL(14,4)     NULL,
  `unidad_id`     INT           NOT NULL,
  `cantidad_base` DECIMAL(14,4)     NULL,
  `fecha`         DATETIME          NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  -- Indice principal: reconstruir el saldo de un producto en una sede.
  KEY `idx_movinv_sucursal_producto_fecha` (`sucursal_id`, `producto_id`, `fecha`),
  KEY `idx_movinv_producto` (`producto_id`),
  KEY `idx_movinv_usuario` (`usuario_id`),
  KEY `idx_movinv_unidad` (`unidad_id`),
  KEY `idx_movinv_fecha` (`fecha`),
  KEY `idx_movinv_motivo` (`motivo`),
  CONSTRAINT `fk_movinv_sucursal`
    FOREIGN KEY (`sucursal_id`)
    REFERENCES `sucursales` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_movinv_producto`
    FOREIGN KEY (`producto_id`)
    REFERENCES `productos` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_movinv_usuario`
    FOREIGN KEY (`usuario_id`)
    REFERENCES `usuarios` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `fk_movinv_unidad`
    FOREIGN KEY (`unidad_id`)
    REFERENCES `unidades_medida` (`id`)
    ON DELETE RESTRICT
    ON UPDATE CASCADE,
  CONSTRAINT `chk_movinv_cantidad`
    CHECK (`cantidad` IS NULL OR `cantidad` > 0),
  CONSTRAINT `chk_movinv_cantidad_base`
    CHECK (`cantidad_base` IS NULL OR `cantidad_base` > 0)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci
  COMMENT = 'Libro mayor: toda entrada y salida de stock, con su responsable';
