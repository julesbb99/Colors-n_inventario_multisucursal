-- =============================================================================
-- 1. Indice unico en LOTES        (evita lotes duplicados)
-- 2. USUARIO_ID en ORDENES_COMPRA (responsable de la compra)
-- 3. Recepcion parcial            (cantidad_recibida + estado nuevo)
--
-- Equivale a la migracion de EF Core
-- `20260917161220_LoteUnicoUsuarioOrdenYRecepcionParcial`.
-- Las dos definiciones deben moverse juntas: si cambias una, cambia la otra, o
-- el esquema que levanta Docker dejara de coincidir con el que espera la API.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. LOTES: un numero de lote identifica UN lote dentro de una sede
--
-- Antes la regla la aplicaba solo el codigo: la recepcion de compras consultaba
-- si el lote existia y, si no, lo creaba. Dos recepciones simultaneas del mismo
-- lote no se ven entre si (ambas consultan antes de que la otra inserte) y
-- terminan creando dos filas, lo que rompe el orden FEFO y el cuadre por lote.
-- Un SELECT ... FOR UPDATE no lo evita: solo bloquea filas existentes, y el
-- problema es justo que todavia no existe ninguna.
--
-- El motor no tiene ese punto ciego: la segunda inserta, falla con el error
-- 1062 y su transaccion se revierte entera.
--
-- OJO: si la base ya tuviera lotes duplicados, esta sentencia falla. Para
-- revisarlo antes:
--   SELECT producto_id, sucursal_id, numero_lote, COUNT(1)
--   FROM lotes GROUP BY 1,2,3 HAVING COUNT(1) > 1;
-- -----------------------------------------------------------------------------
SET @existe_ux := (
  SELECT COUNT(1) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'lotes'
    AND INDEX_NAME   = 'ux_lotes_producto_sucursal_numero');

SET @sql := IF(@existe_ux = 0,
  'CREATE UNIQUE INDEX `ux_lotes_producto_sucursal_numero`
     ON `lotes` (`producto_id`, `sucursal_id`, `numero_lote`)',
  'SELECT ''ux_lotes_producto_sucursal_numero ya existe'' AS aviso');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- -----------------------------------------------------------------------------
-- 2. ORDENES_COMPRA: quien creo la orden
--
-- Hasta ahora ese dato solo sobrevivia en `auditoria_eventos`, donde no se
-- puede consultar desde la orden. RESTRICT en la FK: un usuario con compras a
-- su nombre no se puede borrar, igual que en el libro mayor y la auditoria.
--
-- NOT NULL sin DEFAULT a proposito. Poner DEFAULT 0 sugeriria que el cero es un
-- usuario valido, y no lo es. Sobre una tabla con filas, MySQL rellenaria con
-- cero y la FK rechazaria la sentencia: en ese caso hay que decidir a que
-- usuario se atribuyen las ordenes historicas antes de correr esto.
-- -----------------------------------------------------------------------------
SET @existe_usuario_id := (
  SELECT COUNT(1) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'ordenes_compra'
    AND COLUMN_NAME  = 'usuario_id');

SET @sql := IF(@existe_usuario_id = 0,
  'ALTER TABLE `ordenes_compra`
     ADD COLUMN `usuario_id` INT NOT NULL AFTER `sucursal_id`,
     ADD KEY `idx_oc_usuario` (`usuario_id`),
     ADD CONSTRAINT `fk_oc_usuario`
       FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`)
       ON DELETE RESTRICT ON UPDATE CASCADE',
  'SELECT ''ordenes_compra.usuario_id ya existe'' AS aviso');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- -----------------------------------------------------------------------------
-- 3a. ORDENES_COMPRA: estado 'ParcialmenteRecibida'
--
-- OJO: esta lista RETIRA 'Confirmada', que existia antes y marcaba que el
-- proveedor habia acusado recibo del pedido. Es un concepto distinto de la
-- entrega parcial. Si se necesita, hay que devolverlo aqui Y al enum
-- EstadoOrdenCompra del dominio, que deben coincidir valor por valor: un estado
-- que falte en la base falla con el error 1265 ("Data truncated").
--
-- MODIFY es idempotente por naturaleza: deja la columna en el tipo indicado,
-- se ejecute una vez o diez. Si hubiera filas en 'Confirmada', MySQL las
-- rechazaria; revisar antes con:
--   SELECT COUNT(1) FROM ordenes_compra WHERE estado = 'Confirmada';
-- -----------------------------------------------------------------------------
ALTER TABLE `ordenes_compra`
  MODIFY COLUMN `estado`
    ENUM('Pendiente', 'ParcialmenteRecibida', 'Recibida', 'Cancelada')
    NULL DEFAULT 'Pendiente';


-- -----------------------------------------------------------------------------
-- 3b. ORDEN_COMPRA_DETALLE: cuanto se lleva recibido de cada linea
--
-- Va en la unidad de compra de la linea, la misma de `cantidad`, para poder
-- compararlas sin convertir. Arranca en cero y solo sube; cuando iguala a
-- `cantidad` la linea esta completa.
--
-- OJO con la escala: DECIMAL(12,4) frente a los DECIMAL(14,4) de `cantidad`.
-- El tope de lo recibido queda en 99.999.999,9999 y el de lo pedido es cien
-- veces mayor, asi que una linea por encima de ese tope no se podria completar.
-- Con litros de pintura no se acerca ni de lejos, pero las dos columnas se
-- comparan entre si y lo natural seria que tuvieran la misma escala.
-- -----------------------------------------------------------------------------
SET @existe_recibida := (
  SELECT COUNT(1) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'orden_compra_detalle'
    AND COLUMN_NAME  = 'cantidad_recibida');

SET @sql := IF(@existe_recibida = 0,
  'ALTER TABLE `orden_compra_detalle`
     ADD COLUMN `cantidad_recibida` DECIMAL(12,4) NOT NULL DEFAULT 0.0000
       AFTER `cantidad`,
     -- La invariante de la recepcion parcial, impuesta por el motor: lo
     -- recibido nunca es negativo ni supera lo pedido. El servicio ya lo
     -- valida, pero eso solo cubre lo que pasa por el servicio; esto cubre
     -- tambien un UPDATE hecho a mano.
     ADD CONSTRAINT `chk_ocd_cantidad_recibida`
       CHECK (`cantidad_recibida` >= 0 AND
              (`cantidad` IS NULL OR `cantidad_recibida` <= `cantidad`))',
  'SELECT ''orden_compra_detalle.cantidad_recibida ya existe'' AS aviso');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
