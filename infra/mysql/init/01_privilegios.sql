-- =============================================================================
-- Se ejecuta UNA SOLA VEZ: cuando el volumen `colorsin_mysql_data` está vacío.
-- En ese momento la imagen ya creó `colorsin_inventario` y el usuario `colorsin`.
-- Si cambias este archivo, debes recrear el volumen:
--   docker compose down -v && docker compose up -d
-- =============================================================================

-- Base de datos aparte para pruebas de integración (xUnit / Testcontainers).
CREATE DATABASE IF NOT EXISTS `colorsin_inventario_test`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

-- El usuario de la app manda en ambas bases: EF Core necesita poder crear y
-- borrar tablas para aplicar migraciones.
GRANT ALL PRIVILEGES ON `colorsin_inventario`.*      TO 'colorsin'@'%';
GRANT ALL PRIVILEGES ON `colorsin_inventario_test`.* TO 'colorsin'@'%';

FLUSH PRIVILEGES;
