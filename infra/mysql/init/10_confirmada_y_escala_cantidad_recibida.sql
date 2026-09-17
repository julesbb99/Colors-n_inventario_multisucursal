-- =============================================================================
-- 1. Restaura 'Confirmada' en el ENUM de estado de ORDENES_COMPRA
-- 2. Iguala la escala de ORDEN_COMPRA_DETALLE.cantidad_recibida a (14,4)
--
-- Equivale a la migracion de EF Core
-- `20260917194802_ConfirmadaYEscalaCantidadRecibida`.
-- Las dos definiciones deben moverse juntas: si cambias una, cambia la otra, o
-- el esquema que levanta Docker dejara de coincidir con el que espera la API.
--
-- Idempotente: MODIFY deja la columna en el tipo indicado, se ejecute una vez
-- o diez.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. ESTADO: vuelve 'Confirmada'
--
-- Marca que el proveedor acuso recibo del pedido. Es un concepto distinto de la
-- entrega parcial: con 'Confirmada' todavia no ha llegado mercancia, solo esta
-- confirmado que el pedido se va a surtir. Por eso convive con
-- 'ParcialmenteRecibida' en vez de sustituirla.
--
-- El orden de los valores sigue al del enum EstadoOrdenCompra del dominio, y
-- las dos listas deben coincidir valor por valor: un estado que falte aqui
-- falla al guardarse con el error 1265 ("Data truncated for column 'estado'").
--
-- Los tres estados desde los que se puede recibir son ahora Pendiente,
-- Confirmada y ParcialmenteRecibida.
-- -----------------------------------------------------------------------------
ALTER TABLE `ordenes_compra`
  MODIFY COLUMN `estado`
    ENUM('Pendiente', 'Confirmada', 'ParcialmenteRecibida', 'Recibida', 'Cancelada')
    NULL DEFAULT 'Pendiente';


-- -----------------------------------------------------------------------------
-- 2. CANTIDAD_RECIBIDA: de DECIMAL(12,4) a DECIMAL(14,4)
--
-- Misma escala que `cantidad`, con la que se compara en cada recepcion para
-- decidir si la linea esta completa. Con (12,4) el tope de lo recibido era
-- 99.999.999,9999 y el de lo pedido cien veces mayor, asi que existia un rango
-- de cantidades pedidas que no se podian completar nunca.
--
-- Ampliar la escala no pierde datos: (14,4) contiene a (12,4). El CHECK
-- `chk_ocd_cantidad_recibida` sigue vigente y no hace falta recrearlo.
-- -----------------------------------------------------------------------------
ALTER TABLE `orden_compra_detalle`
  MODIFY COLUMN `cantidad_recibida` DECIMAL(14,4) NOT NULL DEFAULT 0.0000;
