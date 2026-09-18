/**
 * Compras a proveedor. Espeja los DTO de Colorsin.Application/Compras.
 *
 * OJO CON LA RUTA: el recurso es `/api/compras/ordenes`. `/api/compras` a secas
 * no existe y responde 404.
 */

/** Estados tal como los serializa la API. */
export type EstadoOrdenCompra =
  | 'Pendiente'
  | 'ParcialmenteRecibida'
  | 'Recibida'
  | 'Cancelada';

export const ETIQUETA_ESTADO_ORDEN: Record<EstadoOrdenCompra, string> = {
  Pendiente: 'Pendiente',
  ParcialmenteRecibida: 'Parcial',
  Recibida: 'Recibida',
  Cancelada: 'Cancelada',
};

export interface ProveedorDto {
  id: number;
  nombre: string;
  contacto: string | null;
  telefono: string;
  /** Cuantos productos surte. Nulo cuando la consulta no lo calculo. */
  productosQueSurte: number | null;
}

export interface DetalleOrdenCompraDto {
  id: number;
  ordenCompraId: number;
  productoId: number;
  productoNombre: string;
  cantidad: number | null;
  /** Lo que ya llego. Una orden admite recepciones parciales. */
  cantidadRecibida: number;
  cantidadPendiente: number;
  completa: boolean;
  unidadId: number;
  unidadSimbolo: string;
  precioUnitario: number | null;
  descuento: number;
  subtotalBruto: number;
  valorDescuento: number;
  subtotalNeto: number;
}

export interface OrdenCompraDto {
  id: number;
  proveedorId: number;
  proveedorNombre: string;
  sucursalId: number;
  sucursalNombre: string;
  usuarioId: number;
  usuarioNombre: string;
  fecha: string | null;
  estado: EstadoOrdenCompra | null;
  plazoPagoDias: number | null;
  detalles: DetalleOrdenCompraDto[];
  total: number;
  /** Lineas a las que todavia les falta mercancia por llegar. */
  lineasPendientes: number;
}

export interface CrearLineaOrdenCompraDto {
  productoId: number;
  cantidad: number;
  unidadId: number;
  precioUnitario?: number | null;
  descuento?: number;
}

/** El `usuarioId` no va en el cuerpo: sale del token. */
export interface CrearOrdenCompraDto {
  proveedorId: number;
  sucursalId: number;
  plazoPagoDias?: number | null;
  lineas: CrearLineaOrdenCompraDto[];
}

/**
 * Una linea de la recepcion.
 *
 * `cantidad` nula significa "llego todo lo que faltaba de esta linea".
 * `numeroLote` crea o alimenta el lote: si ya existe uno con ese numero en la
 * sede, se le suma cantidad en vez de abrir otro.
 */
export interface LineaRecepcionDto {
  detalleId: number;
  cantidad?: number | null;
  numeroLote?: string | null;
  fechaVencimiento?: string | null;
}

/** Cuerpo vacio -sin `lineas`- significa "llego todo lo que faltaba de la orden". */
export interface ConfirmarRecepcionDto {
  ordenCompraId: number;
  lineas?: LineaRecepcionDto[] | null;
  observaciones?: string | null;
}
