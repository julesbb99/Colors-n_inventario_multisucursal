START TRANSACTION;
ALTER TABLE `ordenes_compra` MODIFY COLUMN `estado` enum('Pendiente','ParcialmenteRecibida','Recibida','Cancelada') COLLATE utf8mb4_0900_ai_ci NULL;

ALTER TABLE `ordenes_compra` ADD `usuario_id` int NOT NULL;

ALTER TABLE `orden_compra_detalle` ADD `cantidad_recibida` decimal(12,4) NOT NULL DEFAULT 0.0;

CREATE INDEX `idx_oc_usuario` ON `ordenes_compra` (`usuario_id`);

ALTER TABLE `orden_compra_detalle` ADD CONSTRAINT `chk_ocd_cantidad_recibida` CHECK (`cantidad_recibida` >= 0 AND (`cantidad` IS NULL OR `cantidad_recibida` <= `cantidad`));

CREATE UNIQUE INDEX `ux_lotes_producto_sucursal_numero` ON `lotes` (`producto_id`, `sucursal_id`, `numero_lote`);

ALTER TABLE `ordenes_compra` ADD CONSTRAINT `fk_oc_usuario` FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260917161220_LoteUnicoUsuarioOrdenYRecepcionParcial', '9.0.20');

COMMIT;

