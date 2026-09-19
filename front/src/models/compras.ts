/**
 * Compras a proveedor. Espeja los DTO de Colorsin.Application/Compras.
 *
 * OJO CON LA RUTA: el recurso es `/api/compras/ordenes`. `/api/compras` a secas
 * no existe y responde 404.
 */

/** Estados tal como los serializa la API. */
export type EstadoOrdenCompra =
  | 'Pendiente'
  | 'Confirmada'
  | 'ParcialmenteRecibida'
  | 'Recibida'
  | 'Cancelada';

export const ETIQUETA_ESTADO_ORDEN: Record<EstadoOrdenCompra, string> = {
  Pendiente: 'Pendiente',
  Confirmada: 'Confirmada',
  ParcialmenteRecibida: 'Parcial',
  Recibida: 'Recibida',
  Cancelada: 'Cancelada',
};

/**
 * Una orden solo se edita o se retira mientras sea un BORRADOR.
 *
 * Desde 'Confirmada' en adelante hay un compromiso con el proveedor y, si entró
 * mercancía, movimientos en el libro mayor que citan esta orden. La API rechaza
 * con 409 cualquier otro estado; esto solo evita ofrecer el botón.
 */
export function esBorrador(estado: EstadoOrdenCompra | null): boolean {
  return estado === 'Pendiente';
}

export interface ProveedorDto {
  id: number;
  nombre: string;
  contacto: string | null;
  telefono: string;
  /** Cuantos productos surte. Nulo cuando la consulta no lo calculo. */
  productosQueSurte: number | null;
  /** `false` si está retirado: no se ofrece en órdenes nuevas, conserva historia. */
  activo: boolean;
}

/** Alta o edición de un proveedor. Solo Administrador General. */
export interface GuardarProveedorDto {
  nombre: string;
  contacto: string | null;
  telefono: string;
}

/**
 * Lo que se sabe del precio de un producto con un proveedor.
 *
 * SON DOS COSAS DISTINTAS Y NO SE MEZCLAN:
 *
 *   precioReferencia  lo pactado en la lista, POR UNIDAD BASE del producto.
 *   ultimaCompra      lo que de verdad se cobró la última vez, CON LA UNIDAD EN
 *                     QUE SE COTIZÓ, sin normalizar, para que la cifra coincida
 *                     con la de la factura.
 *
 * Que no cuadren es justamente la información útil.
 */
export interface PrecioReferenciaDto {
  productoId: number;
  productoNombre: string;
  proveedorId: number;
  proveedorNombre: string;
  /** Unidad en la que viene `precioReferencia`. */
  unidadBaseSimbolo: string | null;
  /** Nulo si ese proveedor no tiene el producto en su lista. No es cero. */
  precioReferencia: number | null;
  ultimaCompra: UltimaCompraDto | null;
}

export interface UltimaCompraDto {
  ordenCompraId: number;
  fecha: string;
  cantidad: number | null;
  unidadId: number;
  unidadSimbolo: string | null;
  precioUnitario: number | null;
  descuento: number;
  estado: string;
}

/** Un precio nulo QUITA el producto de la lista de ese proveedor. */
export interface GuardarPrecioReferenciaDto {
  precioReferencia: number | null;
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
  /**
   * Cuántas líneas tiene la orden.
   *
   * Viene aparte porque `detalles` llega VACÍO en los listados: contar ahí daba
   * cero, y la tabla mostraba «1 de 0».
   */
  lineasTotales: number;
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
