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
