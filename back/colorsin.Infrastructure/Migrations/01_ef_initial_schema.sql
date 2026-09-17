CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
CREATE TABLE `clientes` (
    `id` int NOT NULL AUTO_INCREMENT,
    `razon_social` varchar(150) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `tipo_persona` enum('Natural','Juridica') COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `documento` varchar(20) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `telefono` varchar(20) COLLATE utf8mb4_0900_ai_ci NULL,
    `email` varchar(100) COLLATE utf8mb4_0900_ai_ci NULL,
    `direccion` varchar(150) COLLATE utf8mb4_0900_ai_ci NULL,
    CONSTRAINT `PK_clientes` PRIMARY KEY (`id`)
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `proveedores` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nombre` varchar(100) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `contacto` varchar(100) COLLATE utf8mb4_0900_ai_ci NULL,
    `telefono` varchar(20) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    CONSTRAINT `PK_proveedores` PRIMARY KEY (`id`)
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `sucursales` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nombre` varchar(100) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `ciudad` varchar(80) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `direccion` varchar(150) COLLATE utf8mb4_0900_ai_ci NULL,
    `rol_red` enum('Matriz','Sucursal') COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `descripcion` varchar(200) COLLATE utf8mb4_0900_ai_ci NULL,
    CONSTRAINT `PK_sucursales` PRIMARY KEY (`id`)
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `transportadoras` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nombre` varchar(100) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `tipo_servicio` enum('urgente','estandar') COLLATE utf8mb4_0900_ai_ci NOT NULL,
    CONSTRAINT `PK_transportadoras` PRIMARY KEY (`id`)
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `unidades_medida` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nombre` varchar(50) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `simbolo` varchar(10) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `factor_conversion_litros` decimal(12,6) NULL,
    CONSTRAINT `PK_unidades_medida` PRIMARY KEY (`id`)
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `ordenes_compra` (
    `id` int NOT NULL AUTO_INCREMENT,
    `proveedor_id` int NOT NULL,
    `sucursal_id` int NOT NULL,
    `fecha` datetime NULL DEFAULT CURRENT_TIMESTAMP,
    `estado` enum('Pendiente','Confirmada','Recibida','Cancelada') COLLATE utf8mb4_0900_ai_ci NULL,
    `plazo_pago_dias` int NULL,
    CONSTRAINT `PK_ordenes_compra` PRIMARY KEY (`id`),
    CONSTRAINT `chk_oc_plazo_pago` CHECK (`plazo_pago_dias` IS NULL OR `plazo_pago_dias` >= 0),
    CONSTRAINT `fk_oc_proveedor` FOREIGN KEY (`proveedor_id`) REFERENCES `proveedores` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_oc_sucursal` FOREIGN KEY (`sucursal_id`) REFERENCES `sucursales` (`id`) ON DELETE RESTRICT
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `usuarios` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nombre` varchar(100) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `email` varchar(100) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `password_hash` varchar(255) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `rol` enum('Administrador General','Gerente de Sucursal','Operador') COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `sucursal_id` int NULL,
    CONSTRAINT `PK_usuarios` PRIMARY KEY (`id`),
    CONSTRAINT `fk_usuarios_sucursal` FOREIGN KEY (`sucursal_id`) REFERENCES `sucursales` (`id`) ON DELETE SET NULL
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `productos` (
    `id` int NOT NULL AUTO_INCREMENT,
    `nombre` varchar(100) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `categoria` varchar(50) COLLATE utf8mb4_0900_ai_ci NULL,
    `descripcion` text COLLATE utf8mb4_0900_ai_ci NULL,
    `unidad_base_id` int NULL,
    CONSTRAINT `PK_productos` PRIMARY KEY (`id`),
    CONSTRAINT `fk_productos_unidad_base` FOREIGN KEY (`unidad_base_id`) REFERENCES `unidades_medida` (`id`) ON DELETE RESTRICT
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `ventas` (
    `id` int NOT NULL AUTO_INCREMENT,
    `cliente_id` int NOT NULL,
    `sucursal_id` int NOT NULL,
    `usuario_id` int NOT NULL,
    `fecha` datetime NULL DEFAULT CURRENT_TIMESTAMP,
    `total` decimal(14,2) NULL,
    CONSTRAINT `PK_ventas` PRIMARY KEY (`id`),
    CONSTRAINT `chk_ventas_total` CHECK (`total` IS NULL OR `total` >= 0),
    CONSTRAINT `fk_ventas_cliente` FOREIGN KEY (`cliente_id`) REFERENCES `clientes` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_ventas_sucursal` FOREIGN KEY (`sucursal_id`) REFERENCES `sucursales` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_ventas_usuario` FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`) ON DELETE RESTRICT
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `inventario_sucursal` (
    `id` int NOT NULL AUTO_INCREMENT,
    `sucursal_id` int NOT NULL,
    `producto_id` int NOT NULL,
    `cantidad_base` decimal(14,4) NOT NULL DEFAULT 0.0,
    `stock_minimo` decimal(14,4) NOT NULL DEFAULT 0.0,
    `costo_promedio` decimal(14,4) NOT NULL DEFAULT 0.0,
    CONSTRAINT `PK_inventario_sucursal` PRIMARY KEY (`id`),
    CONSTRAINT `fk_inventario_producto` FOREIGN KEY (`producto_id`) REFERENCES `productos` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_inventario_sucursal` FOREIGN KEY (`sucursal_id`) REFERENCES `sucursales` (`id`) ON DELETE RESTRICT
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `lotes` (
    `id` int NOT NULL AUTO_INCREMENT,
    `producto_id` int NOT NULL,
    `sucursal_id` int NOT NULL,
    `numero_lote` varchar(50) COLLATE utf8mb4_0900_ai_ci NOT NULL,
    `fecha_vencimiento` date NULL,
    `cantidad_base` decimal(14,4) NULL,
    `fecha_ingreso` datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_lotes` PRIMARY KEY (`id`),
    CONSTRAINT `fk_lotes_producto` FOREIGN KEY (`producto_id`) REFERENCES `productos` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_lotes_sucursal` FOREIGN KEY (`sucursal_id`) REFERENCES `sucursales` (`id`) ON DELETE RESTRICT
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `movimientos_inventario` (
    `id` int NOT NULL AUTO_INCREMENT,
    `sucursal_id` int NOT NULL,
    `producto_id` int NOT NULL,
    `usuario_id` int NOT NULL,
    `tipo` enum('Ingreso','Retiro') COLLATE utf8mb4_0900_ai_ci NULL,
    `motivo` enum('Compra','Venta','Ajuste','Transferencia','Merma','Devolucion') COLLATE utf8mb4_0900_ai_ci NULL,
    `cantidad` decimal(14,4) NULL,
    `unidad_id` int NOT NULL,
    `cantidad_base` decimal(14,4) NULL,
    `fecha` datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_movimientos_inventario` PRIMARY KEY (`id`),
    CONSTRAINT `chk_movinv_cantidad` CHECK (`cantidad` IS NULL OR `cantidad` > 0),
    CONSTRAINT `chk_movinv_cantidad_base` CHECK (`cantidad_base` IS NULL OR `cantidad_base` > 0),
    CONSTRAINT `fk_movinv_producto` FOREIGN KEY (`producto_id`) REFERENCES `productos` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_movinv_sucursal` FOREIGN KEY (`sucursal_id`) REFERENCES `sucursales` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_movinv_unidad` FOREIGN KEY (`unidad_id`) REFERENCES `unidades_medida` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_movinv_usuario` FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`) ON DELETE RESTRICT
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `orden_compra_detalle` (
    `id` int NOT NULL AUTO_INCREMENT,
    `orden_compra_id` int NOT NULL,
    `producto_id` int NOT NULL,
    `cantidad` decimal(14,4) NULL,
    `unidad_id` int NOT NULL,
    `precio_unitario` decimal(12,2) NULL,
    `descuento` decimal(5,2) NOT NULL DEFAULT 0.0,
    CONSTRAINT `PK_orden_compra_detalle` PRIMARY KEY (`id`),
    CONSTRAINT `chk_ocd_cantidad` CHECK (`cantidad` IS NULL OR `cantidad` > 0),
    CONSTRAINT `chk_ocd_descuento` CHECK (`descuento` >= 0 AND `descuento` <= 100),
    CONSTRAINT `chk_ocd_precio` CHECK (`precio_unitario` IS NULL OR `precio_unitario` >= 0),
    CONSTRAINT `fk_ocd_orden` FOREIGN KEY (`orden_compra_id`) REFERENCES `ordenes_compra` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_ocd_producto` FOREIGN KEY (`producto_id`) REFERENCES `productos` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_ocd_unidad` FOREIGN KEY (`unidad_id`) REFERENCES `unidades_medida` (`id`) ON DELETE RESTRICT
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `producto_proveedor` (
    `id` int NOT NULL AUTO_INCREMENT,
    `producto_id` int NOT NULL,
    `proveedor_id` int NOT NULL,
    `precio_referencia` decimal(12,2) NULL,
    CONSTRAINT `PK_producto_proveedor` PRIMARY KEY (`id`),
    CONSTRAINT `fk_prodprov_producto` FOREIGN KEY (`producto_id`) REFERENCES `productos` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_prodprov_proveedor` FOREIGN KEY (`proveedor_id`) REFERENCES `proveedores` (`id`) ON DELETE CASCADE
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `transferencias` (
    `id` int NOT NULL AUTO_INCREMENT,
    `producto_id` int NOT NULL,
    `sucursal_origen_id` int NOT NULL,
    `sucursal_destino_id` int NOT NULL,
    `transportadora_id` int NOT NULL,
    `cantidad_solicitada` decimal(14,4) NULL,
    `cantidad_recibida` decimal(14,4) NULL,
    `unidad_id` int NOT NULL,
    `estado` enum('Solicitada','EnPreparacion','EnTransito','RecibidaCompleta','RecibidaParcial') COLLATE utf8mb4_0900_ai_ci NULL,
    `urgencia` enum('Baja','Media','Alta') COLLATE utf8mb4_0900_ai_ci NULL,
    `fecha_solicitud` datetime NULL DEFAULT CURRENT_TIMESTAMP,
    `fecha_estimada_llegada` datetime NULL,
    CONSTRAINT `PK_transferencias` PRIMARY KEY (`id`),
    CONSTRAINT `chk_transf_cantidades` CHECK ((`cantidad_solicitada` IS NULL OR `cantidad_solicitada` > 0) AND (`cantidad_recibida` IS NULL OR `cantidad_recibida` >= 0)),
    CONSTRAINT `chk_transf_sedes_distintas` CHECK (`sucursal_origen_id` <> `sucursal_destino_id`),
    CONSTRAINT `fk_transf_producto` FOREIGN KEY (`producto_id`) REFERENCES `productos` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_transf_sucursal_destino` FOREIGN KEY (`sucursal_destino_id`) REFERENCES `sucursales` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_transf_sucursal_origen` FOREIGN KEY (`sucursal_origen_id`) REFERENCES `sucursales` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_transf_transportadora` FOREIGN KEY (`transportadora_id`) REFERENCES `transportadoras` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_transf_unidad` FOREIGN KEY (`unidad_id`) REFERENCES `unidades_medida` (`id`) ON DELETE RESTRICT
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `venta_detalle` (
    `id` int NOT NULL AUTO_INCREMENT,
    `venta_id` int NOT NULL,
    `producto_id` int NOT NULL,
    `cantidad` decimal(12,2) NULL,
    `unidad_id` int NOT NULL,
    `precio_unitario` decimal(12,2) NULL,
    `descuento` decimal(5,2) NOT NULL DEFAULT 0.0,
    CONSTRAINT `PK_venta_detalle` PRIMARY KEY (`id`),
    CONSTRAINT `chk_vd_cantidad` CHECK (`cantidad` IS NULL OR `cantidad` > 0),
    CONSTRAINT `chk_vd_descuento` CHECK (`descuento` >= 0 AND `descuento` <= 100),
    CONSTRAINT `chk_vd_precio` CHECK (`precio_unitario` IS NULL OR `precio_unitario` >= 0),
    CONSTRAINT `fk_vd_producto` FOREIGN KEY (`producto_id`) REFERENCES `productos` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_vd_unidad` FOREIGN KEY (`unidad_id`) REFERENCES `unidades_medida` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_vd_venta` FOREIGN KEY (`venta_id`) REFERENCES `ventas` (`id`) ON DELETE CASCADE
) COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `novedades_transferencia` (
    `id` int NOT NULL AUTO_INCREMENT,
    `transferencia_id` int NOT NULL,
    `usuario_id` int NOT NULL,
    `tipo` enum('Faltante','Averia','Sobrante','Retraso') COLLATE utf8mb4_0900_ai_ci NULL,
    `cantidad_afectada` decimal(14,4) NULL,
    `observaciones` text COLLATE utf8mb4_0900_ai_ci NULL,
    `fecha` datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_novedades_transferencia` PRIMARY KEY (`id`),
    CONSTRAINT `chk_novtransf_cantidad` CHECK (`cantidad_afectada` IS NULL OR `cantidad_afectada` >= 0),
    CONSTRAINT `fk_novtransf_transferencia` FOREIGN KEY (`transferencia_id`) REFERENCES `transferencias` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_novtransf_usuario` FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`) ON DELETE RESTRICT
) COLLATE=utf8mb4_0900_ai_ci;

CREATE UNIQUE INDEX `uq_clientes_documento` ON `clientes` (`documento`);

CREATE INDEX `IX_inventario_sucursal_producto_id` ON `inventario_sucursal` (`producto_id`);

CREATE UNIQUE INDEX `uq_inventario_sucursal_producto` ON `inventario_sucursal` (`sucursal_id`, `producto_id`);

CREATE INDEX `idx_lotes_numero` ON `lotes` (`numero_lote`);

CREATE INDEX `idx_lotes_vencimiento` ON `lotes` (`fecha_vencimiento`);

CREATE INDEX `IX_lotes_producto_id` ON `lotes` (`producto_id`);

CREATE INDEX `IX_lotes_sucursal_id` ON `lotes` (`sucursal_id`);

CREATE INDEX `idx_movinv_sucursal_producto_fecha` ON `movimientos_inventario` (`sucursal_id`, `producto_id`, `fecha`);

CREATE INDEX `IX_movimientos_inventario_producto_id` ON `movimientos_inventario` (`producto_id`);

CREATE INDEX `IX_movimientos_inventario_unidad_id` ON `movimientos_inventario` (`unidad_id`);

CREATE INDEX `IX_movimientos_inventario_usuario_id` ON `movimientos_inventario` (`usuario_id`);

CREATE INDEX `IX_novedades_transferencia_transferencia_id` ON `novedades_transferencia` (`transferencia_id`);

CREATE INDEX `IX_novedades_transferencia_usuario_id` ON `novedades_transferencia` (`usuario_id`);

CREATE INDEX `IX_orden_compra_detalle_orden_compra_id` ON `orden_compra_detalle` (`orden_compra_id`);

CREATE INDEX `IX_orden_compra_detalle_producto_id` ON `orden_compra_detalle` (`producto_id`);

CREATE INDEX `IX_orden_compra_detalle_unidad_id` ON `orden_compra_detalle` (`unidad_id`);

CREATE INDEX `idx_oc_estado` ON `ordenes_compra` (`estado`);

CREATE INDEX `idx_oc_sucursal_fecha` ON `ordenes_compra` (`sucursal_id`, `fecha`);

CREATE INDEX `IX_ordenes_compra_proveedor_id` ON `ordenes_compra` (`proveedor_id`);

CREATE INDEX `IX_producto_proveedor_proveedor_id` ON `producto_proveedor` (`proveedor_id`);

CREATE UNIQUE INDEX `uq_producto_proveedor` ON `producto_proveedor` (`producto_id`, `proveedor_id`);

CREATE INDEX `IX_productos_unidad_base_id` ON `productos` (`unidad_base_id`);

CREATE INDEX `idx_transf_destino_estado` ON `transferencias` (`sucursal_destino_id`, `estado`);

CREATE INDEX `IX_transferencias_producto_id` ON `transferencias` (`producto_id`);

CREATE INDEX `IX_transferencias_sucursal_origen_id` ON `transferencias` (`sucursal_origen_id`);

CREATE INDEX `IX_transferencias_transportadora_id` ON `transferencias` (`transportadora_id`);

CREATE INDEX `IX_transferencias_unidad_id` ON `transferencias` (`unidad_id`);

CREATE INDEX `IX_usuarios_sucursal_id` ON `usuarios` (`sucursal_id`);

CREATE UNIQUE INDEX `uq_usuarios_email` ON `usuarios` (`email`);

CREATE INDEX `IX_venta_detalle_producto_id` ON `venta_detalle` (`producto_id`);

CREATE INDEX `IX_venta_detalle_unidad_id` ON `venta_detalle` (`unidad_id`);

CREATE INDEX `IX_venta_detalle_venta_id` ON `venta_detalle` (`venta_id`);

CREATE INDEX `idx_ventas_sucursal_fecha` ON `ventas` (`sucursal_id`, `fecha`);

CREATE INDEX `IX_ventas_cliente_id` ON `ventas` (`cliente_id`);

CREATE INDEX `IX_ventas_usuario_id` ON `ventas` (`usuario_id`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260917010821_InitialCreate', '9.0.20');

COMMIT;

