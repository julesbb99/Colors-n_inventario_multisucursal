/**
 * Catalogo de productos. Espeja ProductoDto.
 *
 * NO SE FILTRA POR SEDE: el catalogo es de la red. Que una sede no tenga saldo
 * de un producto no significa que no lo maneje.
 */
export interface ProductoDto {
  id: number;
  nombre: string;
  categoria: string | null;
  descripcion: string | null;
  unidadBaseId: number | null;
  unidadBaseNombre: string | null;
  unidadBaseSimbolo: string | null;
  /**
   * Precio de venta POR UNIDAD BASE, para toda la red. Nulo si no se ha fijado,
   * que NO es lo mismo que cero.
   *
   * Viaja en el catálogo -que ya está cargado- para que el formulario de venta
   * pueda rellenar el precio en cuanto se elige el producto, sin una consulta
   * por línea. Se edita en Ventas → Lista de precios, solo el administrador.
   */
  precioVenta: number | null;
}

/**
 * Saldo de una pareja (sede, producto). Espeja InventarioSucursalDto.
 *
 * `cantidadBase` NO esta en litros sino en la unidad base DE CADA PRODUCTO; por
 * eso viaja `unidadBaseSimbolo`. Sumar esta columna entre productos distintos
 * mezclaria unidades.
 */
export interface ExistenciaDto {
  id: number;
  sucursalId: number;
  sucursalNombre: string;
  productoId: number;
  productoNombre: string;
  unidadBaseSimbolo: string | null;
  cantidadBase: number;
  stockMinimo: number;
  costoPromedio: number;
  /**
   * Criterio de la API: `cantidadBase <= stockMinimo`, Y ADEMÁS activa.
   * Una existencia deshabilitada nunca alerta, por bajo que esté su saldo.
   */
  enAlerta: boolean;
  /**
   * `false` si está dada de baja lógica: la sede dejó de manejar el producto.
   *
   * NO es lo mismo que saldo cero. Cero significa "se maneja y se agotó";
   * inactiva significa "esta sede ya no lo maneja". La fila sigue en la base con
   * su saldo, sus lotes y su historia: nada se borra.
   */
  activo: boolean;
}

/**
 * Alta de una existencia. SIN CANTIDAD, igual que la API.
 *
 * El saldo solo se mueve por el libro mayor, donde cada asiento dice de dónde
 * salió la mercancía. Nace en cero y se llena con un ingreso, una recepción de
 * compra o un traslado.
 */
export interface CrearExistenciaDto {
  sucursalId: number;
  productoId: number;
  stockMinimo: number;
}

/** Edición: solo el mínimo. La cantidad y el costo no se digitan. */
export interface ActualizarExistenciaDto {
  stockMinimo: number;
}

/** Los estados de un saldo. Son EXCLUYENTES: una fila está en uno solo. */
export type EstadoExistencia = 'deshabilitado' | 'agotado' | 'alerta' | 'conSaldo';

/**
 * El estado de un saldo, en un solo sitio.
 *
 * OJO CON `enAlerta`: el criterio de la API es `cantidadBase <= stockMinimo`, y
 * un saldo en CERO también lo cumple. Es correcto por parte del servidor -no hay
 * nada, desde luego que falta stock- pero para la pantalla no vale: un producto
 * agotado saldría a la vez en "En alerta" y en "Agotadas", contado dos veces y
 * listado dos veces, que fue justo lo que se vio en producción.
 *
 * Por eso "alerta" aquí significa BAJO PERO CON SALDO, y el orden de las
 * comprobaciones es el que lo garantiza: agotado primero.
 *
 * Y "deshabilitado" va ANTES que todo lo demás: una fila dada de baja se
 * describe por eso y no por su saldo. Mostrarla como "agotada" invitaría a
 * reponer un producto que la sede decidió dejar de manejar.
 *
 * Vive en el modelo y no en la tabla a propósito: la insignia ya distinguía los
 * dos casos, pero las pestañas contaban con otro criterio. Con una sola función
 * no pueden volver a discrepar.
 */
export function estadoExistencia(fila: ExistenciaDto): EstadoExistencia {
  if (!fila.activo) {
    return 'deshabilitado';
  }
  if (fila.cantidadBase <= 0) {
    return 'agotado';
  }
  if (fila.enAlerta) {
    return 'alerta';
  }
  return 'conSaldo';
}

/**
 * Lote de un producto en una sede. Espeja LoteDto.
 *
 * Un lote se crea VACIO: la mercancia entra despues con un movimiento de
 * ingreso o una recepcion de compra, que son las vias que ademas mueven el
 * consolidado de la sede y dejan fila en el libro mayor.
 */
export interface LoteDto {
  id: number;
  productoId: number;
  productoNombre: string;
  sucursalId: number;
  sucursalNombre: string;
  numeroLote: string;
  /**
   * Siempre viene. Todo lo que vende Colorsin caduca, la columna es NOT NULL y
   * tanto la recepcion de compras como la correccion de un lote la exigen.
   */
  fechaVencimiento: string;
  cantidadBase: number | null;
  unidadBaseSimbolo: string | null;
  fechaIngreso: string | null;
  /** Negativo si ya vencio. Lo calcula la API. */
  diasParaVencer: number | null;
  vencido: boolean;
}

// Aqui estaban TIPO_MOVIMIENTO, MOTIVO_MOVIMIENTO y RegistrarMovimientoDto. Se
// fueron con el ajuste manual de stock, que ya no existe en ningun rol: eran de
// ENTRADA -lo que se mandaba al crear un movimiento a mano- y no hay quien los
// mande. Los movimientos se siguen LEYENDO en el libro mayor, pero ahi el tipo y
// el motivo llegan como texto ya resuelto en `MovimientoDto`, sin ordinales.

// Aqui estaba CrearLoteDto. Ya no hay alta de lotes: nacen al recibir una compra
// o un traslado, con el numero que trae el envase.

/**
 * Correccion de un lote.
 *
 * Es un PUT, asi que el cuerpo describe como debe quedar el lote: hay que
 * mandar los dos campos, aunque solo cambie uno. La caducidad se puede
 * corregir, pero NO borrar.
 */
export interface ActualizarLoteDto {
  numeroLote: string;
  fechaVencimiento: string;
}

/** Los tres estados de caducidad que pinta la interfaz. */
export type EstadoCaducidad = 'Vigente' | 'Por vencer' | 'Vencido';

/**
 * Traduce un lote a su estado visual.
 *
 * El umbral entra por parametro y no se fija aqui: lo manda la API en
 * `ResumenGeneralDto.diasUmbralVencimiento`, para que la pantalla no pueda
 * clasificar con un numero distinto del que uso el servidor para contar.
 */
export function estadoCaducidad(
  lote: LoteDto,
  diasUmbral: number,
): EstadoCaducidad {
  if (lote.vencido) {
    return 'Vencido';
  }
  if (lote.diasParaVencer !== null && lote.diasParaVencer <= diasUmbral) {
    return 'Por vencer';
  }
  return 'Vigente';
}
