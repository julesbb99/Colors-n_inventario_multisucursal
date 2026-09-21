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
  | 'Cerrada'
  | 'Rechazada'
  | 'Cancelada';

/** Etiquetas para mostrar. El enum viene sin espacios y no se lee bien en una tabla. */
export const ETIQUETA_ESTADO_TRANSFERENCIA: Record<EstadoTransferencia, string> = {
  Solicitada: 'Solicitada',
  EnTransito: 'En tránsito',
  Completada: 'Completada',
  RecibidaParcial: 'Recibida parcial',
  // «Cerrada con faltante» y no «Cerrada» a secas: en una tabla, junto a
  // «Completada», hay que poder distinguir de un vistazo la que llegó entera de
  // la que se dio por terminada sabiendo que faltó mercancía.
  Cerrada: 'Cerrada con faltante',
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

// Aquí estaba NOVEDADES_CRITICAS, la lista de las que solo podía registrar
// supervisión. Ya no existe esa distinción: una novedad no mueve stock, es el
// testimonio de quien descargó, y exigirle rango solo conseguía que el faltante
// lo anotara -tarde- alguien que no estuvo en la descarga.

export interface TransportadoraDto {
  id: number;
  nombre: string;
  /** 'urgente' o 'estandar', en minúscula, como el ENUM de la base. */
  tipoServicio: string;
  /**
   * Días que tarda en entregar. Es lo que convierte la etiqueta del servicio en
   * una fecha: al elegirla en el despacho, la llegada estimada se calcula sola.
   *
   * Va por transportadora y no por tipo de servicio porque «urgente» es una
   * etiqueta comercial, no un plazo: dos urgentes pueden tardar 1 y 2 días.
   */
  diasEntrega: number;
  /**
   * `false` si está retirada: no se ofrece al despachar, pero sigue citada en
   * los traslados que llevó, con su guía y su fecha.
   *
   * Es baja lógica, no borrado: `transferencias.transportadora_id` la
   * referencia, y ese dato es el que hace falta el día que se reclama un
   * faltante.
   */
  activo: boolean;
}

/**
 * Alta o edición de una transportadora.
 *
 * Sirve para las dos: el id va en la ruta, no en el cuerpo.
 */
export interface GuardarTransportadoraDto {
  nombre: string;
  /** 'urgente' o 'estandar'. La API no distingue mayúsculas al entrar. */
  tipoServicio: 'urgente' | 'estandar';
  /** Al menos 1. Es lo que calcula la fecha estimada de llegada. */
  diasEntrega: number;
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
  /**
   * OBLIGATORIA. La API responde 400 si falta o si es anterior a hoy.
   *
   * Sigue declarada como opcional en el tipo porque el campo del formulario
   * empieza vacío y se rellena al elegir transportadora; lo que no se admite es
   * enviarla vacía.
   */
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
