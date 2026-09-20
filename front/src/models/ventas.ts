/** Ventas de mostrador. Espeja los DTO de Colorsin.Application/Ventas. */

export interface ClienteDto {
  id: number;
  razonSocial: string;
  /** 'Natural' o 'Juridica'. */
  tipoPersona: string;
  /** Cedula o NIT. Es por lo que se busca en el mostrador. */
  documento: string;
  telefono: string | null;
  email: string | null;
  direccion: string | null;
}

/**
 * Alta de un cliente desde el mostrador.
 *
 * Sin `id` -lo asigna la base- y sin sede: un cliente le compra a la red, no a
 * una bodega. Lo que pertenece a una sede es la venta.
 */
export interface CrearClienteDto {
  razonSocial: string;
  /** 'Natural' o 'Juridica'. Va como texto, igual que sale en `ClienteDto`. */
  tipoPersona: 'Natural' | 'Juridica';
  /** Cédula o NIT. Único en toda la base: la API responde 409 si se repite. */
  documento: string;
  telefono?: string | null;
  email?: string | null;
  direccion?: string | null;
}

/**
 * A cuánto se vende un producto. Espeja `PrecioVentaDto` de la API.
 *
 * SON TRES CIFRAS DISTINTAS Y NO SE FUNDEN EN UNA:
 *
 *   precioVenta    lo que la administración fijó. POR UNIDAD BASE.
 *   ultimaVenta    lo que de verdad se cobró la última vez, EN LA UNIDAD EN QUE
 *                  SE COTIZÓ, sin normalizar, para que coincida con la factura.
 *   costoPromedio  lo que ha costado en bodega. POR UNIDAD BASE.
 *
 * El costo NO es un precio de venta: vender al costo es vender sin margen. Está
 * para comparar.
 */
export interface PrecioVentaDto {
  productoId: number;
  productoNombre: string;
  /** Unidad de `precioVenta` y `costoPromedio`. */
  unidadBaseSimbolo: string | null;
  /** Nulo si nadie lo ha fijado. NO es cero: cero sería regalarlo. */
  precioVenta: number | null;
  ultimaVenta: UltimaVentaDto | null;
  costoPromedio: number | null;
  /** Cuánto deja sobre el costo. NEGATIVO significa vender con pérdida. */
  margenPorcentaje: number | null;
}

export interface UltimaVentaDto {
  ventaId: number;
  fecha: string | null;
  cantidad: number | null;
  unidadId: number;
  unidadSimbolo: string | null;
  precioUnitario: number | null;
  descuento: number;
}

/** Un precio nulo QUITA el producto de la lista; no es lo mismo que cero. */
export interface GuardarPrecioVentaDto {
  precioVenta: number | null;
}

export interface DetalleVentaDto {
  id: number;
  ventaId: number;
  productoId: number;
  productoNombre: string;
  cantidad: number | null;
  unidadId: number;
  unidadSimbolo: string;
  precioUnitario: number | null;
  descuento: number;
  subtotalBruto: number;
  valorDescuento: number;
  subtotalNeto: number;
}

export interface VentaDto {
  id: number;
  clienteId: number;
  clienteRazonSocial: string;
  clienteDocumento: string;
  sucursalId: number;
  sucursalNombre: string;
  usuarioId: number;
  usuarioNombre: string;
  fecha: string | null;
  total: number | null;
  detalles: DetalleVentaDto[];
}

export interface CrearLineaVentaDto {
  productoId: number;
  cantidad: number;
  unidadId: number;
  /**
   * Precio por la unidad de ESTA línea.
   *
   * Omitido o nulo, el servidor usa el de lista del producto convertido a esta
   * unidad. Si el producto tampoco lo tiene, RECHAZA la venta: antes la dejaba
   * pasar con la columna en nulo y el total la contaba como cero, así que la
   * venta quedaba registrada regalada y con el stock descontado.
   */
  precioUnitario?: number | null;
  descuento?: number;
  /** Lote concreto. Omitido, el servidor descuenta por FEFO, que es lo normal. */
  loteId?: number | null;
}

/** El `usuarioId` no va en el cuerpo: sale del token. */
export interface CrearVentaDto {
  clienteId: number;
  sucursalId: number;
  lineas: CrearLineaVentaDto[];
  observaciones?: string | null;
}

/** De que lote salio cada porcion de una linea, en orden FEFO. */
export interface ConsumoLoteDto {
  loteId: number | null;
  numeroLote: string | null;
  fechaVencimiento: string | null;
  cantidadBase: number;
  movimientoId: number;
}

export interface LineaVendidaDto {
  detalleId: number;
  productoId: number;
  productoNombre: string;
  cantidad: number;
  unidadId: number;
  cantidadBase: number;
  consumos: ConsumoLoteDto[];
}

export interface SaldoAfectadoDto {
  productoId: number;
  productoNombre: string;
  cantidadBaseDescontada: number;
  saldoResultante: number;
}

/** Lo que devuelve POST /api/ventas: que se vendio y de que lotes salio. */
export interface VentaRegistradaDto {
  ventaId: number;
  total: number;
  lineas: LineaVendidaDto[];
  saldos: SaldoAfectadoDto[];
}
