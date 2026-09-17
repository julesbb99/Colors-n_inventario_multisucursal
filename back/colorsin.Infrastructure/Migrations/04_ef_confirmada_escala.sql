START TRANSACTION;
ALTER TABLE `ordenes_compra` MODIFY COLUMN `estado` enum('Pendiente','Confirmada','ParcialmenteRecibida','Recibida','Cancelada') COLLATE utf8mb4_0900_ai_ci NULL;

ALTER TABLE `orden_compra_detalle` MODIFY COLUMN `cantidad_recibida` decimal(14,4) NOT NULL DEFAULT 0.0;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260917194802_ConfirmadaYEscalaCantidadRecibida', '9.0.20');

COMMIT;

