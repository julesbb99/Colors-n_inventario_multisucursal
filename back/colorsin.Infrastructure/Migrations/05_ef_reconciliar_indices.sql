START TRANSACTION;
SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'inventario_sucursal' AND INDEX_NAME = 'IX_inventario_sucursal_producto_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'inventario_sucursal' AND INDEX_NAME = 'idx_inventario_producto');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `inventario_sucursal` RENAME INDEX `IX_inventario_sucursal_producto_id` TO `idx_inventario_producto`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'lotes' AND INDEX_NAME = 'IX_lotes_sucursal_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'lotes' AND INDEX_NAME = 'idx_lotes_sucursal');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `lotes` RENAME INDEX `IX_lotes_sucursal_id` TO `idx_lotes_sucursal`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'movimientos_inventario' AND INDEX_NAME = 'IX_movimientos_inventario_producto_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'movimientos_inventario' AND INDEX_NAME = 'idx_movinv_producto');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `movimientos_inventario` RENAME INDEX `IX_movimientos_inventario_producto_id` TO `idx_movinv_producto`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'movimientos_inventario' AND INDEX_NAME = 'IX_movimientos_inventario_unidad_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'movimientos_inventario' AND INDEX_NAME = 'idx_movinv_unidad');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `movimientos_inventario` RENAME INDEX `IX_movimientos_inventario_unidad_id` TO `idx_movinv_unidad`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'movimientos_inventario' AND INDEX_NAME = 'IX_movimientos_inventario_usuario_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'movimientos_inventario' AND INDEX_NAME = 'idx_movinv_usuario');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `movimientos_inventario` RENAME INDEX `IX_movimientos_inventario_usuario_id` TO `idx_movinv_usuario`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'novedades_transferencia' AND INDEX_NAME = 'IX_novedades_transferencia_transferencia_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'novedades_transferencia' AND INDEX_NAME = 'idx_novtransf_transferencia');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `novedades_transferencia` RENAME INDEX `IX_novedades_transferencia_transferencia_id` TO `idx_novtransf_transferencia`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'novedades_transferencia' AND INDEX_NAME = 'IX_novedades_transferencia_usuario_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'novedades_transferencia' AND INDEX_NAME = 'idx_novtransf_usuario');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `novedades_transferencia` RENAME INDEX `IX_novedades_transferencia_usuario_id` TO `idx_novtransf_usuario`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'orden_compra_detalle' AND INDEX_NAME = 'IX_orden_compra_detalle_orden_compra_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'orden_compra_detalle' AND INDEX_NAME = 'idx_ocd_orden');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `orden_compra_detalle` RENAME INDEX `IX_orden_compra_detalle_orden_compra_id` TO `idx_ocd_orden`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'orden_compra_detalle' AND INDEX_NAME = 'IX_orden_compra_detalle_producto_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'orden_compra_detalle' AND INDEX_NAME = 'idx_ocd_producto');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `orden_compra_detalle` RENAME INDEX `IX_orden_compra_detalle_producto_id` TO `idx_ocd_producto`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'orden_compra_detalle' AND INDEX_NAME = 'IX_orden_compra_detalle_unidad_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'orden_compra_detalle' AND INDEX_NAME = 'idx_ocd_unidad');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `orden_compra_detalle` RENAME INDEX `IX_orden_compra_detalle_unidad_id` TO `idx_ocd_unidad`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'ordenes_compra' AND INDEX_NAME = 'IX_ordenes_compra_proveedor_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'ordenes_compra' AND INDEX_NAME = 'idx_oc_proveedor');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `ordenes_compra` RENAME INDEX `IX_ordenes_compra_proveedor_id` TO `idx_oc_proveedor`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'producto_proveedor' AND INDEX_NAME = 'IX_producto_proveedor_proveedor_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'producto_proveedor' AND INDEX_NAME = 'idx_prodprov_proveedor');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `producto_proveedor` RENAME INDEX `IX_producto_proveedor_proveedor_id` TO `idx_prodprov_proveedor`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'productos' AND INDEX_NAME = 'IX_productos_unidad_base_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'productos' AND INDEX_NAME = 'idx_productos_unidad_base');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `productos` RENAME INDEX `IX_productos_unidad_base_id` TO `idx_productos_unidad_base`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'transferencias' AND INDEX_NAME = 'IX_transferencias_producto_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'transferencias' AND INDEX_NAME = 'idx_transf_producto');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `transferencias` RENAME INDEX `IX_transferencias_producto_id` TO `idx_transf_producto`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'transferencias' AND INDEX_NAME = 'IX_transferencias_sucursal_origen_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'transferencias' AND INDEX_NAME = 'idx_transf_origen');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `transferencias` RENAME INDEX `IX_transferencias_sucursal_origen_id` TO `idx_transf_origen`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'transferencias' AND INDEX_NAME = 'IX_transferencias_transportadora_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'transferencias' AND INDEX_NAME = 'idx_transf_transportadora');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `transferencias` RENAME INDEX `IX_transferencias_transportadora_id` TO `idx_transf_transportadora`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'transferencias' AND INDEX_NAME = 'IX_transferencias_unidad_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'transferencias' AND INDEX_NAME = 'idx_transf_unidad');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `transferencias` RENAME INDEX `IX_transferencias_unidad_id` TO `idx_transf_unidad`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'usuarios' AND INDEX_NAME = 'IX_usuarios_sucursal_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'usuarios' AND INDEX_NAME = 'idx_usuarios_sucursal');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `usuarios` RENAME INDEX `IX_usuarios_sucursal_id` TO `idx_usuarios_sucursal`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'venta_detalle' AND INDEX_NAME = 'IX_venta_detalle_producto_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'venta_detalle' AND INDEX_NAME = 'idx_vd_producto');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `venta_detalle` RENAME INDEX `IX_venta_detalle_producto_id` TO `idx_vd_producto`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'venta_detalle' AND INDEX_NAME = 'IX_venta_detalle_unidad_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'venta_detalle' AND INDEX_NAME = 'idx_vd_unidad');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `venta_detalle` RENAME INDEX `IX_venta_detalle_unidad_id` TO `idx_vd_unidad`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'venta_detalle' AND INDEX_NAME = 'IX_venta_detalle_venta_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'venta_detalle' AND INDEX_NAME = 'idx_vd_venta');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `venta_detalle` RENAME INDEX `IX_venta_detalle_venta_id` TO `idx_vd_venta`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'ventas' AND INDEX_NAME = 'IX_ventas_cliente_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'ventas' AND INDEX_NAME = 'idx_ventas_cliente');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `ventas` RENAME INDEX `IX_ventas_cliente_id` TO `idx_ventas_cliente`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @origen := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'ventas' AND INDEX_NAME = 'IX_ventas_usuario_id');
SET @destino := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'ventas' AND INDEX_NAME = 'idx_ventas_usuario');
SET @sql := IF(@origen > 0 AND @destino = 0,
    'ALTER TABLE `ventas` RENAME INDEX `IX_ventas_usuario_id` TO `idx_ventas_usuario`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @existe := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'lotes' AND INDEX_NAME = 'idx_lotes_producto_sucursal');
SET @sql := IF(@existe = 0,
    'CREATE INDEX `idx_lotes_producto_sucursal` ON `lotes` (`producto_id`, `sucursal_id`)',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @existe := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'movimientos_inventario' AND INDEX_NAME = 'idx_movinv_fecha');
SET @sql := IF(@existe = 0,
    'CREATE INDEX `idx_movinv_fecha` ON `movimientos_inventario` (`fecha`)',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @existe := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'movimientos_inventario' AND INDEX_NAME = 'idx_movinv_motivo');
SET @sql := IF(@existe = 0,
    'CREATE INDEX `idx_movinv_motivo` ON `movimientos_inventario` (`motivo`)',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @existe := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'novedades_transferencia' AND INDEX_NAME = 'idx_novtransf_tipo');
SET @sql := IF(@existe = 0,
    'CREATE INDEX `idx_novtransf_tipo` ON `novedades_transferencia` (`tipo`)',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @existe := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'transferencias' AND INDEX_NAME = 'idx_transf_estado');
SET @sql := IF(@existe = 0,
    'CREATE INDEX `idx_transf_estado` ON `transferencias` (`estado`)',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @existe := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'ventas' AND INDEX_NAME = 'idx_ventas_fecha');
SET @sql := IF(@existe = 0,
    'CREATE INDEX `idx_ventas_fecha` ON `ventas` (`fecha`)',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @existe := (SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'lotes' AND INDEX_NAME = 'IX_lotes_producto_id');
SET @sql := IF(@existe > 0,
    'DROP INDEX `IX_lotes_producto_id` ON `lotes`',
    'DO 0');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260917204752_ReconciliarNombresDeIndices', '9.0.20');

COMMIT;

