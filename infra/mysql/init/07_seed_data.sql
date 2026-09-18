-- =============================================================================
-- Colorsin S.A.S. - Inventario
-- SEED 01: Datos iniciales de prueba
--
-- Poblacion base de la red: 3 sedes, 3 unidades de medida, 5 usuarios,
-- 2 proveedores, 3 clientes, 3 productos, 2 transportadoras, 9 saldos de
-- inventario y 11 lotes.
--
-- ORDEN DE LAS SECCIONES
--   Sigue las dependencias de clave foranea, agrupado por modulo:
--     Comun (unidades, sucursales, usuarios)
--       -> Compras (proveedores)
--       -> Ventas (clientes)
--       -> Inventario (productos, transportadoras, vinculos, saldos, lotes)
--   `usuarios` va despues de `sucursales` porque la referencia; `productos`
--   despues de `unidades_medida`; `inventario_sucursal` y `lotes` al final
--   porque dependen de sedes y productos.
--
-- UNIDAD DE ALMACENAMIENTO: LITRO.
--   Todo `cantidad_base`, `stock_minimo` y `costo_promedio` esta expresado en
--   litros, tal como vienen los datos de origen. Las equivalencias en galones
--   son referencia comercial y salen de `unidades_medida.factor_conversion_litros`.
--
-- IDEMPOTENTE: usa ids explicitos + ON DUPLICATE KEY UPDATE, asi que se puede
-- correr las veces que haga falta sin duplicar filas. El AUTO_INCREMENT sigue
-- funcionando normal para los registros que se creen despues.
-- =============================================================================

USE `colorsin_inventario`;
SET NAMES utf8mb4;

START TRANSACTION;

-- =============================================================================
-- PASO 1 - MODULO COMUN
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. UNIDADES DE MEDIDA
--    El litro es la unidad base del sistema: factor 1.0. Todo lo demas se
--    convierte contra el (1 galon US = 3.78541 L; 1 caneca = 5 galones).
-- -----------------------------------------------------------------------------
INSERT INTO `unidades_medida` (`id`, `nombre`, `simbolo`, `factor_conversion_litros`) VALUES
  (1, 'Litro',            'L',   1.000000),
  (2, 'Galón',            'gal', 3.785410),
  (3, 'Caneca 5 galones', 'cn5', 18.927050)
AS nuevo
ON DUPLICATE KEY UPDATE
  `nombre`                   = nuevo.`nombre`,
  `simbolo`                  = nuevo.`simbolo`,
  `factor_conversion_litros` = nuevo.`factor_conversion_litros`;


-- -----------------------------------------------------------------------------
-- 2. SUCURSALES
--    Cali es la Matriz de la red; Armenia y Manizales son sucursales.
--    El departamento va en `descripcion` porque el modelo solo tiene `ciudad`.
-- -----------------------------------------------------------------------------
INSERT INTO `sucursales` (`id`, `nombre`, `ciudad`, `direccion`, `rol_red`, `descripcion`) VALUES
  (1, 'Colorsin Principal',   'Cali',      'Calle 25 # 4-58, Zona Industrial',  'Matriz',
      'Sede matriz de la red. Valle del Cauca. Centro de distribución principal.'),
  (2, 'Colorsin Eje Cafetero','Armenia',   'Carrera 19 # 12-40, Parque Industrial', 'Sucursal',
      'Sucursal del eje cafetero. Quindío.'),
  (3, 'Colorsin Manizales',   'Manizales', 'Avenida Santander # 32-15',          'Sucursal',
      'Sucursal de Manizales. Caldas.')
AS nuevo
ON DUPLICATE KEY UPDATE
  `nombre`      = nuevo.`nombre`,
  `ciudad`      = nuevo.`ciudad`,
  `direccion`   = nuevo.`direccion`,
  `rol_red`     = nuevo.`rol_red`,
  `descripcion` = nuevo.`descripcion`;


-- -----------------------------------------------------------------------------
-- 3. USUARIOS
--
--    Los tres roles quedan representados, que es lo que hace falta para probar
--    la trazabilidad de `usuario_id` en movimientos_inventario, ordenes_compra,
--    ventas y auditoria_eventos: esas cuatro tablas tienen FK obligatoria a
--    esta, asi que sin usuarios no se puede registrar NADA.
--
--    Reparto por sede, pensado para poder probar el filtro por sucursal:
--      Cali      -> 1 gerente + 1 operador
--      Armenia   -> 1 gerente
--      Manizales -> 1 operador
--
--    `sucursal_id` NULO en el Administrador General: no pertenece a una sede
--    concreta, manda sobre toda la red. La columna es nullable justamente por
--    este caso.
--
--    ================== CONTRASENAS: LEER ANTES DE DESPLEGAR ==================
--
--    Estas cinco cuentas SI pueden iniciar sesion, con claves de DESARROLLO que
--    estan escritas aqui abajo y por tanto en el repositorio. Es a proposito:
--    sin credenciales que funcionen no hay forma de probar el sistema, y el
--    marcador que habia antes impedia entrar a todo el mundo.
--
--    Lo que eso significa: CUALQUIERA QUE VEA ESTE ARCHIVO PUEDE ENTRAR a
--    cualquier entorno donde se haya corrido este script sin cambiar las
--    claves. Antes de exponer la API a una red que no sea la tuya, cambialas.
--
--      id 1  marcela.ospina@colorsin.com.co   Colorsin.Dev.Admin1
--      id 2  julian.restrepo@colorsin.com.co  Colorsin.Dev.Gerente1
--      id 3  diana.carvajal@colorsin.com.co   Colorsin.Dev.Gerente2
--      id 4  hector.zapata@colorsin.com.co    Colorsin.Dev.Operador1
--      id 5  paola.guerrero@colorsin.com.co   Colorsin.Dev.Operador2
--
--    Los hash son BCrypt con costo 12 ($2a$12$...), calculados con el mismo
--    servicio que usa la API. Llevan la sal dentro, asi que los cinco son
--    distintos aunque las claves se parezcan, y volver a generarlos da valores
--    distintos cada vez: no se pueden comparar dos hash entre si.
--
--    Una clave por usuario, y no una compartida, para que probar el filtro por
--    sede no obligue a compartir credencial entre roles.
--
--    Para cambiar una: genera el hash con el mismo algoritmo y actualiza la
--    fila. NO escribas la contrasena en claro en la columna: el verificador la
--    rechazaria igual, porque no tiene formato de hash.
--
--    OJO, el caso que ya estaba documentado aqui y sigue vigente: si una fila
--    queda con algo que no es un hash valido, BCrypt.Net lanza
--    SaltParseException. El verificador de Colorsin lo atrapa y lo trata como
--    credenciales incorrectas; si algun dia se cambia de libreria, hay que
--    conservar ese comportamiento o un dato malo saldra como error 500.
-- -----------------------------------------------------------------------------
INSERT INTO `usuarios` (`id`, `nombre`, `email`, `password_hash`, `rol`, `sucursal_id`) VALUES
  -- clave de desarrollo: Colorsin.Dev.Admin1
  (1, 'Marcela Ospina Rivera',  'marcela.ospina@colorsin.com.co',
      '$2a$12$RQaJKbPITv0WzikM7dJFoOKmLWAU/JxHLYc9MAUHuMOGNGTjnspEW', 'Administrador General', NULL),
  -- clave de desarrollo: Colorsin.Dev.Gerente1
  (2, 'Julián Restrepo Cano',   'julian.restrepo@colorsin.com.co',
      '$2a$12$OGC2/iNVss14vtfVpNk/gOcG2.gHZ.wWg7mS5QInNvv/PeXb960oO', 'Gerente de Sucursal',   1),
  -- clave de desarrollo: Colorsin.Dev.Gerente2
  (3, 'Diana Carvajal Londoño', 'diana.carvajal@colorsin.com.co',
      '$2a$12$.tOSx7cY1UPYR/6uZuOp3ermS52Sa4L4tvip9AzRlxbq2w78AyZI2', 'Gerente de Sucursal',   2),
  -- clave de desarrollo: Colorsin.Dev.Operador1
  (4, 'Héctor Zapata Muñoz',    'hector.zapata@colorsin.com.co',
      '$2a$12$0mkFNvEdno2dmXKAQKGtIOCVry8LaQm99ravYub5hWkDviA89hFLu', 'Operador',              1),
  -- clave de desarrollo: Colorsin.Dev.Operador2
  (5, 'Paola Guerrero Salas',   'paola.guerrero@colorsin.com.co',
      '$2a$12$R8qGJZXWnyA/I3kqPRJgGu/zXknXKIu14P/PexWn9sBfPOo7B90NC', 'Operador',              3)
AS nuevo
ON DUPLICATE KEY UPDATE
  `nombre`      = nuevo.`nombre`,
  `email`       = nuevo.`email`,
  `rol`         = nuevo.`rol`,
  `sucursal_id` = nuevo.`sucursal_id`;
  -- `password_hash` queda FUERA del UPDATE a proposito: si alguien ya definio
  -- la clave real de un usuario, volver a correr el seed no debe borrarsela.


-- =============================================================================
-- PASO 2 - MODULO COMPRAS
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 4. PROVEEDORES
--    Necesarios para poder crear ordenes de compra: `ordenes_compra` tiene FK
--    a esta tabla.
-- -----------------------------------------------------------------------------
INSERT INTO `proveedores` (`id`, `nombre`, `contacto`, `telefono`) VALUES
  (1, 'Pinturas y Resinas de Colombia',   'Laura Méndez',   '6017894522'),
  (2, 'Solventes Petroquímicos del Neusa','Andrés Beltrán', '6015213398')
AS nuevo
ON DUPLICATE KEY UPDATE
  `nombre`   = nuevo.`nombre`,
  `contacto` = nuevo.`contacto`,
  `telefono` = nuevo.`telefono`;


-- =============================================================================
-- PASO 3 - MODULO VENTAS
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 5. CLIENTES
--    Necesarios para poder registrar ventas: `ventas` tiene FK a esta tabla.
--
--    `documento` es UNICO (uq_clientes_documento): NIT con digito de
--    verificacion en las personas juridicas, cedula en la natural. Mezclar los
--    dos formatos a proposito, para que las validaciones que se escriban
--    despues se topen con los dos casos.
-- -----------------------------------------------------------------------------
INSERT INTO `clientes`
  (`id`, `razon_social`, `tipo_persona`, `documento`, `telefono`, `email`, `direccion`) VALUES
  (1, 'Constructora Andina del Pacífico S.A.S.', 'Juridica', '901345678-2',
      '6024412200', 'compras@andinapacifico.com.co',
      'Calle 5 # 38-115, Oficina 802, Cali'),
  (2, 'Ferretería La Herramienta Ltda.',         'Juridica', '890912345-7',
      '6067451188', 'pedidos@laherramienta.com.co',
      'Carrera 23 # 18-44, Manizales'),
  (3, 'Wilson Andrés Corrales Pineda',           'Natural',  '1094567821',
      '3156678990', 'wacorrales@gmail.com',
      'Carrera 14 # 9-27, Barrio La Castellana, Armenia')
AS nuevo
ON DUPLICATE KEY UPDATE
  `razon_social` = nuevo.`razon_social`,
  `tipo_persona` = nuevo.`tipo_persona`,
  `documento`    = nuevo.`documento`,
  `telefono`     = nuevo.`telefono`,
  `email`        = nuevo.`email`,
  `direccion`    = nuevo.`direccion`;


-- =============================================================================
-- PASO 4 - MODULO INVENTARIO
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 6. PRODUCTOS
--    `unidad_base_id` = 1 (Litro) en los tres: es la unidad en la que se lleva
--    el stock. Ver la nota sobre galones al final de este archivo.
--
--    OJO: `stock_minimo` NO vive aqui. Es una columna de `inventario_sucursal`
--    (seccion 9), porque el umbral de reposicion es por SEDE: Cali y Manizales
--    pueden tener minimos distintos del mismo producto.
-- -----------------------------------------------------------------------------
INSERT INTO `productos` (`id`, `nombre`, `categoria`, `descripcion`, `unidad_base_id`) VALUES
  (1, 'Pintura Epóxica Industrial', 'Recubrimiento',
      'Recubrimiento epóxico de dos componentes para superficies metálicas y concreto en ambiente industrial. Se comercializa en galones; el stock se lleva en litros.',
      1),
  (2, 'Disolvente Thinner Multiuso', 'Solvente',
      'Solvente de limpieza y dilución para esmaltes y recubrimientos. Producto inflamable: requiere almacenamiento ventilado.',
      1),
  (3, 'Esmalte Sintético Anticorrosivo', 'Recubrimiento',
      'Esmalte sintético con inhibidor de corrosión para estructura metálica expuesta. Se comercializa en galones; el stock se lleva en litros.',
      1)
AS nuevo
ON DUPLICATE KEY UPDATE
  `nombre`         = nuevo.`nombre`,
  `categoria`      = nuevo.`categoria`,
  `descripcion`    = nuevo.`descripcion`,
  `unidad_base_id` = nuevo.`unidad_base_id`;


-- -----------------------------------------------------------------------------
-- 7. TRANSPORTADORAS
--    Los valores del ENUM son 'urgente' y 'estandar' en minuscula, tal como
--    quedaron definidos en el DDL.
-- -----------------------------------------------------------------------------
INSERT INTO `transportadoras` (`id`, `nombre`, `tipo_servicio`) VALUES
  (1, 'Envío Express Químicos', 'urgente'),
  (2, 'Redetrans S.A.',         'estandar')
AS nuevo
ON DUPLICATE KEY UPDATE
  `nombre`        = nuevo.`nombre`,
  `tipo_servicio` = nuevo.`tipo_servicio`;


-- -----------------------------------------------------------------------------
-- 8. VINCULO PRODUCTO - PROVEEDOR
--    Quien surte que. Precio de referencia por LITRO, coherente con el
--    costo_promedio del inventario.
-- -----------------------------------------------------------------------------
INSERT INTO `producto_proveedor` (`id`, `producto_id`, `proveedor_id`, `precio_referencia`) VALUES
  (1, 1, 1, 48880.00),  -- Pintura Epóxica <- Pinturas y Resinas
  (2, 3, 1, 37530.00),  -- Esmalte         <- Pinturas y Resinas
  (3, 2, 2, 22500.00)   -- Thinner         <- Solventes Petroquímicos
AS nuevo
ON DUPLICATE KEY UPDATE
  `producto_id`       = nuevo.`producto_id`,
  `proveedor_id`      = nuevo.`proveedor_id`,
  `precio_referencia` = nuevo.`precio_referencia`;


-- -----------------------------------------------------------------------------
-- 9. INVENTARIO INICIAL POR SUCURSAL
--    Todo en LITROS. Aqui vive `stock_minimo`, el umbral de reposicion por
--    sede. Tres situaciones deliberadamente distintas para poder probar las
--    alertas de stock:
--      * Cali      -> por encima del minimo en los tres productos.
--      * Armenia   -> por DEBAJO del minimo en los tres (227.1<380, 38<150, 204.4<300).
--      * Manizales -> Pintura Epóxica AGOTADA (0.0), Thinner sobre el minimo,
--                     Esmalte con buen saldo.
-- -----------------------------------------------------------------------------
INSERT INTO `inventario_sucursal`
  (`id`, `sucursal_id`, `producto_id`, `cantidad_base`, `stock_minimo`, `costo_promedio`) VALUES
  -- Cali (Matriz)
  (1, 1, 1, 1590.5000, 380.0000, 48880.0000),
  (2, 1, 2,  540.0000, 150.0000, 22500.0000),
  (3, 1, 3,  984.6000, 300.0000, 37530.0000),
  -- Armenia (Eje Cafetero) - las tres por debajo del minimo
  (4, 2, 1,  227.1000, 380.0000, 48880.0000),
  (5, 2, 2,   38.0000, 150.0000, 22500.0000),
  (6, 2, 3,  204.4000, 300.0000, 37530.0000),
  -- Manizales - Pintura Epóxica agotada
  (7, 3, 1,    0.0000, 380.0000, 48880.0000),
  (8, 3, 2,  300.0000, 150.0000, 22500.0000),
  (9, 3, 3,  984.6000, 300.0000, 37530.0000)
AS nuevo
ON DUPLICATE KEY UPDATE
  `cantidad_base`  = nuevo.`cantidad_base`,
  `stock_minimo`   = nuevo.`stock_minimo`,
  `costo_promedio` = nuevo.`costo_promedio`;


-- -----------------------------------------------------------------------------
-- 10. LOTES
--    Trazabilidad del saldo anterior. La suma de los lotes de cada pareja
--    (producto, sucursal) CUADRA con `inventario_sucursal.cantidad_base`.
--
--    Manizales / Pintura Epóxica no tiene lotes: su saldo es 0 (agotado).
--
--    Vencimientos tipicos del sector: recubrimientos ~24 meses, solventes ~36.
--    El lote ESA-2024-118 de Cali vence el 2026-11-30 a proposito, para poder
--    probar la alerta FEFO de proximos a vencer.
-- -----------------------------------------------------------------------------
INSERT INTO `lotes`
  (`id`, `producto_id`, `sucursal_id`, `numero_lote`, `fecha_vencimiento`, `cantidad_base`, `fecha_ingreso`) VALUES
  -- Cali / Pintura Epóxica -> 1000.0 + 590.5 = 1590.5
  ( 1, 1, 1, 'PEI-2026-041', '2028-03-15', 1000.0000, '2026-03-12 08:30:00'),
  ( 2, 1, 1, 'PEI-2026-077', '2028-07-20',  590.5000, '2026-07-18 09:15:00'),
  -- Cali / Thinner -> 540.0
  ( 3, 2, 1, 'DTM-2026-053', '2029-05-30',  540.0000, '2026-05-26 14:00:00'),
  -- Cali / Esmalte -> 600.0 + 384.6 = 984.6
  ( 4, 3, 1, 'ESA-2026-062', '2028-06-10',  600.0000, '2026-06-08 10:45:00'),
  ( 5, 3, 1, 'ESA-2024-118', '2026-11-30',  384.6000, '2024-11-22 11:20:00'),
  -- Armenia / Pintura Epóxica -> 227.1
  ( 6, 1, 2, 'PEI-2026-084', '2028-08-05',  227.1000, '2026-08-03 07:50:00'),
  -- Armenia / Thinner -> 38.0
  ( 7, 2, 2, 'DTM-2026-071', '2029-07-14',   38.0000, '2026-07-10 16:30:00'),
  -- Armenia / Esmalte -> 204.4
  ( 8, 3, 2, 'ESA-2026-069', '2028-07-02',  204.4000, '2026-06-29 13:10:00'),
  -- Manizales / Thinner -> 300.0
  ( 9, 2, 3, 'DTM-2026-088', '2029-08-22',  300.0000, '2026-08-19 09:40:00'),
  -- Manizales / Esmalte -> 500.0 + 484.6 = 984.6
  (10, 3, 3, 'ESA-2026-050', '2028-05-18',  500.0000, '2026-05-15 08:05:00'),
  (11, 3, 3, 'ESA-2026-090', '2028-09-01',  484.6000, '2026-08-28 15:25:00')
AS nuevo
ON DUPLICATE KEY UPDATE
  `producto_id`       = nuevo.`producto_id`,
  `sucursal_id`       = nuevo.`sucursal_id`,
  `numero_lote`       = nuevo.`numero_lote`,
  `fecha_vencimiento` = nuevo.`fecha_vencimiento`,
  `cantidad_base`     = nuevo.`cantidad_base`,
  `fecha_ingreso`     = nuevo.`fecha_ingreso`;

COMMIT;

-- =============================================================================
-- NOTA SOBRE LA UNIDAD BASE DE LOS PRODUCTOS
--
-- La especificacion pedia unidad base "Galón" para la Pintura Epóxica y el
-- Esmalte, pero entregaba TODAS las cantidades en litros:
--   cantidad_base (L) = 1590.5, stock_minimo (L) = 380, costo = $48.880/L
--
-- Si `unidad_base_id` apuntara a Galón, esos 1590.5 se leerian como 1590.5
-- GALONES (= 6.021 L), casi cuatro veces el stock real. Por eso los tres
-- productos quedaron con unidad base LITRO, que es coherente con los datos y
-- con "Unidad base: Litro = 1.0" de la especificacion.
--
-- El galon sigue disponible en `unidades_medida` para comprar, vender y
-- transferir: esas tablas tienen su propio `unidad_id` y la conversion se hace
-- con `factor_conversion_litros`. La unidad comercial no se pierde.
-- =============================================================================
