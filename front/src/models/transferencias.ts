/**
 * Traslados entre sedes. Espeja los DTO de Colorsin.Application/Transferencias.
 *
 * OJO CON EL NOMBRE: el modulo se llama TRANSFERENCIAS en la API
 * (`/api/transferencias`). "Traslados" es como lo llama la interfaz, y solo la
 * interfaz: `/api/traslados` no existe y responde 404.
 */

/** Estados tal como los serializa la API (el `ToString()` del enum del dominio). */
export type EstadoTransferencia =
  | 'Solicitada'
  | 'EnTransito'
  | 'Completada'
  | 'RecibidaParcial'
  | 'Rechazada'
  | 'Cancelada';

/** Etiquetas para mostrar. El enum viene sin espacios y no se lee bien en una tabla. */
export const ETIQUETA_ESTADO_TRANSFERENCIA: Record<EstadoTransferencia, string> = {
  Solicitada: 'Solicitada',
  EnTransito: 'En tránsito',
  Completada: 'Completada',
  RecibidaParcial: 'Recibida parcial',
  Rechazada: 'Rechazada',
  Cancelada: 'Cancelada',
};

/**
 * LOS ENUM DE ENTRADA VIAJAN COMO NUMERO, no como texto.
 *
 * La API no registra `JsonStringEnumConverter`, asi que System.Text.Json espera
 * el valor ordinal en el cuerpo de la peticion. Mandar `"Media"` produce un 400
 * que no dice cual campo fallo. Las RESPUESTAS si traen texto, porque los DTO de
 * salida ya declaran esas propiedades como `string`.
 *
 * El orden es el del enum del dominio y no se puede reordenar sin romper esto.
 */
export const URGENCIA = { baja: 0, media: 1, alta: 2 } as const;
export type ValorUrgencia = (typeof URGENCIA)[keyof typeof URGENCIA];

export const TIPO_NOVEDAD = {
  faltante: 0,
  averia: 1,
  sobrante: 2,
  retraso: 3,
} as const;
export type ValorTipoNovedad = (typeof TIPO_NOVEDAD)[keyof typeof TIPO_NOVEDAD];

/** Una novedad critica solo la puede registrar supervision; la API responde 403 al operador. */
export const NOVEDADES_CRITICAS: readonly ValorTipoNovedad[] = [
  TIPO_NOVEDAD.faltante,
  TIPO_NOVEDAD.averia,
];

export interface TransportadoraDto {
  id: number;
  nombre: string;
  tipoServicio: string;
}

/** Un movimiento de inventario ligado al traslado: la salida del origen o la entrada al destino. */
export interface DetalleTransferenciaDto {
  movimientoId: number;
  tipo: string | null;
  sucursalId: number;
  sucursalNombre: string;
  loteId: number | null;
  numeroLote: string | null;
  fechaVencimiento: string | null;
  cantidadBase: number | null;
  fecha: string | null;
}

export interface NovedadTransferenciaDto {
  id: number;
  transferenciaId: number;
  usuarioId: number;
  usuarioNombre: string;
  tipo: string | null;
  cantidadAfectada: number | null;
  observaciones: string | null;
  fecha: string | null;
}

export interface TransferenciaDto {
  id: number;
  productoId: number;
  productoNombre: string;
  sucursalOrigenId: number;
  sucursalOrigenNombre: string;
  sucursalDestinoId: number;
  sucursalDestinoNombre: string;
  /** Quien la SOLICITO. El despacho y la recepcion quedan en los movimientos. */
  usuarioId: number;
  usuarioNombre: string;
  transportadoraId: number | null;
  transportadoraNombre: string | null;
  guia: string | null;
  cantidadSolicitada: number | null;
  /** Nula hasta que el destino la recibe. Menor que la solicitada = faltante. */
  cantidadRecibida: number | null;
  unidadId: number;
  unidadSimbolo: string;
  estado: EstadoTransferencia | null;
  urgencia: string | null;
  fechaSolicitud: string | null;
  fechaEstimadaLlegada: string | null;
  movimientos: DetalleTransferenciaDto[];
  novedades: NovedadTransferenciaDto[];
}

/** La pide la sede DESTINO. El `usuarioId` no va: sale del token. */
export interface CrearTransferenciaDto {
  productoId: number;
  sucursalOrigenId: number;
  sucursalDestinoId: number;
  cantidad: number;
  unidadId: number;
  urgencia?: ValorUrgencia;
}

/** Lo hace la sede ORIGEN. Descuenta stock por FEFO. */
export interface DespacharTransferenciaDto {
  transferenciaId: number;
  transportadoraId: number;
  guia: string;
  fechaEstimadaLlegada?: string | null;
  observaciones?: string | null;
}

/** Lo hace la sede DESTINO. Sin `cantidadRecibida` llega todo lo despachado. */
export interface RecibirTransferenciaDto {
  transferenciaId: number;
  cantidadRecibida?: number | null;
  observaciones?: string | null;
}

export interface RegistrarNovedadDto {
  transferenciaId: number;
  tipo: ValorTipoNovedad;
  cantidadAfectada?: number | null;
  observaciones?: string | null;
}

/** Motivo opcional de un rechazo o una cancelacion. */
export interface CierreTransferenciaDto {
  motivo?: string | null;
}
