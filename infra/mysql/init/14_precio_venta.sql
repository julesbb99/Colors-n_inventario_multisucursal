-- =============================================================================
-- MODULO VENTAS: precio de venta por producto
--
-- Anade `productos.precio_venta`, EXPRESADO POR UNIDAD BASE del producto, igual
-- que `producto_proveedor.precio_referencia` lo esta del lado de la compra.
--
-- POR QUE HACIA FALTA. Hasta ahora la unica tabla de precios era la de COMPRA.
-- Quien vendia tenia que teclear el precio de memoria, y el campo no decia en
-- que unidad iba, asi que el mismo producto salio a precios que no se parecen:
--
--     venta 4   1 cn5  a $5.000.000  ->  $264.172 por litro
--     venta 5   2 gal  a $   50.000  ->  $ 13.209 por litro   (costo: $37.530)
--
-- La segunda se vendio POR DEBAJO DEL COSTO, con unos $24.000 de perdida por
-- litro, y nadie lo noto: 50.000 parece una cifra razonable si no se cae en que
-- es por galon. Con un precio de lista por unidad base, la pantalla puede
-- convertirlo a la unidad de cada linea y rellenarlo sola.
--
-- POR UNIDAD BASE Y NO "EN LA UNIDAD EN QUE SE VENDE". Guardar el precio junto a
-- una unidad obligaria a convertir en los dos sentidos y a decidir que pasa
-- cuando alguien vende en otra; con la unidad base hay UNA cifra canonica y
-- todas las demas se derivan de ella con el catalogo `unidades_medida`.
--
-- DE TODA LA RED, NO POR SEDE. Es la misma decision que en la lista de precios
-- de proveedor: el catalogo lo mantiene el Administrador General y las tres
-- sedes lo heredan. Si manana hace falta un precio por sede, se agrega una tabla
-- aparte; esta columna seguiria siendo el precio base.
--
-- NULL SIGNIFICA "SIN PRECIO FIJADO", que no es lo mismo que gratis. Por eso el
-- CHECK exige que, cuando haya valor, sea mayor que cero: un cero en esta
-- columna solo puede ser un error de digitacion, y si se colara la pantalla
-- rellenaria las ventas con cero pesos.
--
-- Idempotente: se puede ejecutar a mano sobre una base ya creada.
-- =============================================================================

SET @existe := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'productos'
    AND COLUMN_NAME  = 'precio_venta'
);

SET @sql := IF(@existe = 0,
  'ALTER TABLE `productos`
     ADD COLUMN `precio_venta` DECIMAL(12,2) NULL
     COMMENT ''Precio de venta POR UNIDAD BASE. NULL = sin fijar, no es gratis''
     AFTER `unidad_base_id`,
     ADD CONSTRAINT `chk_productos_precio_venta`
       CHECK (`precio_venta` IS NULL OR `precio_venta` > 0)',
  'SELECT ''La columna productos.precio_venta ya existe'' AS aviso');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
