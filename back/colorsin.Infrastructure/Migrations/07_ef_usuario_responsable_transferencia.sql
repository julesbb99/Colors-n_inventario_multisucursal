START TRANSACTION;
ALTER TABLE `transferencias` ADD `usuario_id` int NOT NULL;

CREATE INDEX `idx_transf_usuario` ON `transferencias` (`usuario_id`);

ALTER TABLE `transferencias` ADD CONSTRAINT `fk_transf_usuario` FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260917212802_UsuarioResponsableTransferencia', '9.0.20');

COMMIT;

