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
  /** Criterio de la API: `cantidadBase <= stockMinimo`. */
  enAlerta: boolean;
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
  /** Nula en productos que no caducan: van al final de la cola FEFO. */
  fechaVencimiento: string | null;
  cantidadBase: number | null;
  unidadBaseSimbolo: string | null;
  fechaIngreso: string | null;
  /** Negativo si ya vencio, nulo si el lote no caduca. Lo calcula la API. */
  diasParaVencer: number | null;
  vencido: boolean;
}

/**
 * LOS ENUM DE ENTRADA VIAJAN COMO NUMERO.
 *
 * La API no registra `JsonStringEnumConverter`, asi que System.Text.Json espera
 * el valor ordinal. Mandar `"Ingreso"` da un 400 que no dice cual campo fallo.
 * El orden es el del enum del dominio y no se puede reordenar sin romper esto.
 */
export const TIPO_MOVIMIENTO = { ingreso: 0, retiro: 1 } as const;
export type ValorTipoMovimiento = (typeof TIPO_MOVIMIENTO)[keyof typeof TIPO_MOVIMIENTO];

export const MOTIVO_MOVIMIENTO = {
  compra: 0,
  venta: 1,
  ajuste: 2,
  transferencia: 3,
  merma: 4,
  devolucion: 5,
} as const;
export type ValorMotivoMovimiento =
  (typeof MOTIVO_MOVIMIENTO)[keyof typeof MOTIVO_MOVIMIENTO];

/**
 * Un movimiento manual. El `usuarioId` NO va aqui: sale del token.
 *
 * Restringido a supervision: lo que entra por compras, sale por ventas o se
 * mueve por traslados tiene su propio documento detras. Un movimiento a mano es
 * la unica forma de cambiar el stock sin uno.
 */
export interface RegistrarMovimientoDto {
  sucursalId: number;
  productoId: number;
  tipoMovimiento: ValorTipoMovimiento;
  motivo: ValorMotivoMovimiento;
  cantidad: number;
  unidadId: number;
  /** Imputa el movimiento a un lote concreto. Opcional. */
  loteId?: number | null;
  observaciones?: string | null;
}

/** Alta de un lote. Sin cantidad: nace vacio. */
export interface CrearLoteDto {
  productoId: number;
  sucursalId: number;
  numeroLote: string;
  fechaVencimiento?: string | null;
  fechaIngreso?: string | null;
}

/** Correccion de un lote. Es un PUT: `fechaVencimiento` nula BORRA la fecha. */
export interface ActualizarLoteDto {
  numeroLote: string;
  fechaVencimiento?: string | null;
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
