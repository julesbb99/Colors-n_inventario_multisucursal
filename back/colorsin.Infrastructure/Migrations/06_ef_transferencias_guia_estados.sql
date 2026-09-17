START TRANSACTION;
ALTER TABLE `transferencias` MODIFY COLUMN `transportadora_id` int NULL;

ALTER TABLE `transferencias` MODIFY COLUMN `estado` enum('Solicitada','EnTransito','Completada','RecibidaParcial','Rechazada','Cancelada') COLLATE utf8mb4_0900_ai_ci NULL;

ALTER TABLE `transferencias` ADD `guia` varchar(50) COLLATE utf8mb4_0900_ai_ci NULL;

ALTER TABLE `movimientos_inventario` ADD `transferencia_id` int NULL;

CREATE INDEX `idx_movinv_transferencia` ON `movimientos_inventario` (`transferencia_id`);

ALTER TABLE `movimientos_inventario` ADD CONSTRAINT `fk_movinv_transferencia` FOREIGN KEY (`transferencia_id`) REFERENCES `transferencias` (`id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260917211412_TransferenciasGuiaYEstados', '9.0.20');

COMMIT;

