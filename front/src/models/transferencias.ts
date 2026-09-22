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

/**
 * QUÉ SE VA A HACER con lo que se reportó, y con ello si el traslado cierra.
 *
 * Los dos del medio dejan trabajo por delante -alguien tiene que volver a
 * mandar la mercancía, o pelear el cobro- así que la novedad queda ABIERTA y el
 * traslado sigue «por recibir». Los otros dos no esperan nada.
 */
export const TRATAMIENTO_NOVEDAD = {
  ninguno: 0,
  reenvio: 1,
  reclamacion: 2,
  asumido: 3,
} as const;
export type ValorTratamientoNovedad =
  (typeof TRATAMIENTO_NOVEDAD)[keyof typeof TRATAMIENTO_NOVEDAD];

/** Como los serializa la API: el `ToString()` del enum del dominio. */
export type TratamientoNovedad = 'Ninguno' | 'Reenvio' | 'Reclamacion' | 'Asumido';

/** Con tilde y con eñe, que el ENUM de MySQL no lleva. */
export const ETIQUETA_TRATAMIENTO: Record<TratamientoNovedad, string> = {
  Ninguno: 'Solo constancia',
  Reenvio: 'Reenvío',
  Reclamacion: 'Reclamación',
  Asumido: 'Asumido como merma',
};

export type EstadoNovedad = 'Abierta' | 'Cerrada';

/** Tipos tal como los serializa la API, y cómo se escriben bien. */
export type TipoNovedad = 'Faltante' | 'Averia' | 'Sobrante' | 'Retraso';

export const ETIQUETA_TIPO_NOVEDAD: Record<TipoNovedad, string> = {
  Faltante: 'Faltante',
  Averia: 'Avería',
  Sobrante: 'Sobrante',
  Retraso: 'Retraso',
};

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

/**
 * Un lote que viaja en el traslado, con lo que salió de él.
 *
 * SÍ VIAJA EN EL LISTADO, a diferencia de los movimientos: son pocos por
 * traslado y son lo que permite cotejar el camión contra el papel. Sin ellos la
 * tabla solo puede decir «Pintura Epóxica», que no distingue una tanda de otra
 * ni dice qué vence.
 *
 * Sale de los movimientos de DESPACHO. El destino recrea esos mismos números al
 * recibir, así que no hay dos listas: es la misma mercancía.
 */
export interface LoteTrasladadoDto {
  /** Lote del origen. Nulo si la cantidad salió sin lote asignado. */
  loteId: number | null;
  /** El número del fabricante. Nulo en el mismo caso; se muestra «sin lote». */
  numeroLote: string | null;
  fechaVencimiento: string | null;
  /** Cuánto salió de ESE lote, en unidad base. La suma es lo despachado. */
  cantidadBase: number;
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
  tipo: TipoNovedad | null;
  cantidadAfectada: number | null;
  /** Qué se decidió hacer. Reenvío y Reclamación dejan el traslado pendiente. */
  tratamiento: TratamientoNovedad;
  estado: EstadoNovedad;
  /** El porqué del cierre. Nulo mientras sigue abierta. */
  motivoCierre: string | null;
  fechaCierre: string | null;
  /** Quien la cerró. Suele no ser quien la reportó: entre ambas cosas pasan días. */
  usuarioCierreNombre: string | null;
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
  /**
   * Lo que DE VERDAD salió del origen. Nula hasta el despacho.
   *
   * Menor que la solicitada significa que el origen no tenía todo. Esa
   * diferencia NO es una pérdida: sigue en su estante.
   */
  cantidadDespachada: number | null;
  /**
   * Nula hasta que el destino la recibe.
   *
   * Se compara contra la DESPACHADA, no contra la solicitada: si mandaron 3 de
   * los 5 pedidos y llegaron 3, el traslado llegó completo.
   */
  cantidadRecibida: number | null;
  unidadId: number;
  unidadSimbolo: string;
  /**
   * Unidad BASE del producto, que puede NO ser la del traslado.
   *
   * ES LA DE LAS CANTIDADES DE `lotes`. Un traslado de 5 GALONES mueve 18,93
   * LITROS de lote, así que rotular ese 18,93 con `unidadSimbolo` diría «18,93
   * gal»: un error de un factor 3,785 justo en la cifra que alguien va a
   * cotejar contra el envase.
   */
  unidadBaseSimbolo: string | null;
  estado: EstadoTransferencia | null;
  urgencia: string | null;
  fechaSolicitud: string | null;
  fechaDespacho: string | null;
  fechaEstimadaLlegada: string | null;
  fechaRecepcion: string | null;
  /** Novedades que siguen esperando desenlace. Viaja también en el listado. */
  novedadesAbiertas: number;
  /**
   * Los lotes que viajan. Vacía mientras está «Solicitada»: hasta el despacho
   * no ha salido nada, así que no hay lote que nombrar.
   */
  lotes: LoteTrasladadoDto[];
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
   *
   * Es además la fecha que HABILITA la recepción: hasta ese día el destino no
   * puede recibir.
   */
  fechaEstimadaLlegada?: string | null;
  /**
   * Lo que de verdad se manda, cuando no es todo lo pedido.
   *
   * Nula despacha lo solicitado, que es el caso normal. Mayor que lo solicitado
   * la API lo rechaza: el ajuste del origen solo va hacia abajo.
   */
  cantidadDespachada?: number | null;
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
  /**
   * Número, no texto: la API espera el ordinal del enum.
   *
   * `reenvio` y `reclamacion` dejan la novedad abierta y el traslado sigue
   * «por recibir»; `ninguno` y `asumido` la cierran al nacer.
   */
  tratamiento?: ValorTratamientoNovedad;
  observaciones?: string | null;
}

/** Motivo opcional de un rechazo o una cancelacion. */
export interface CierreTransferenciaDto {
  motivo?: string | null;
}

/** Cierre de una novedad pendiente. El motivo es OBLIGATORIO: la API responde 400 sin él. */
export interface CerrarNovedadDto {
  motivo: string;
}

/**
 * Cumplimiento logístico de un grupo de traslados: una sede, o una ruta.
 *
 * TODO SON CONTEOS, nunca volúmenes: cada traslado lleva su producto en su
 * unidad, y sumar litros con galones daría un número sin significado.
 */
export interface CumplimientoGrupoDto {
  clave: string;
  etiqueta: string;
  sucursalOrigenId: number | null;
  sucursalDestinoId: number | null;
  solicitados: number;
  despachados: number;
  rechazados: number;
  cancelados: number;
  enCurso: number;
  recibidos: number;
  completos: number;
  parciales: number;
  aTiempo: number;
  tarde: number;
  /** Despachos en que el origen mandó menos de lo pedido. No es culpa del transporte. */
  ajustadosEnOrigen: number;
  conNovedad: number;
  novedadesAbiertas: number;
  diasTransitoPromedio: number | null;
  /** Despachados / (solicitados − cancelados). Mide a la BODEGA. */
  cumplimientoAtencion: number | null;
  /** Completos / recibidos. Mide al TRANSPORTE en mercancía. */
  cumplimientoCantidad: number | null;
  /** A tiempo / recibidos con plazo. Mide al TRANSPORTE en tiempo. */
  cumplimientoPlazo: number | null;
}

export interface ReporteCumplimientoDto {
  desde: string | null;
  hasta: string | null;
  total: CumplimientoGrupoDto;
  /** Una fila por sede, contando lo que DESPACHA: es quien responde por el traslado. */
  porSucursal: CumplimientoGrupoDto[];
  porRuta: CumplimientoGrupoDto[];
}

/**
 * Cómo le fue a un traslado frente a su plazo.
 *
 * SON SEIS CASOS Y NO DOS: un traslado que aún no ha salido no es un
 * incumplimiento, y uno sin fecha estimada no se puede juzgar.
 */
export type EstadoPlazo =
  | 'SinDespachar'
  | 'EnTransito'
  | 'ATiempo'
  | 'Tarde'
  | 'SinPlazo'
  | 'NoAplica';

export const ETIQUETA_PLAZO: Record<EstadoPlazo, string> = {
  SinDespachar: 'Sin despachar',
  EnTransito: 'En camino',
  ATiempo: 'A tiempo',
  Tarde: 'Tarde',
  SinPlazo: 'Sin plazo',
  NoAplica: 'No aplica',
};

/** Una novedad, con lo justo para una celda. */
export interface NovedadResumenDto {
  tipo: TipoNovedad | null;
  cantidadAfectada: number | null;
  tratamiento: TratamientoNovedad;
  estado: EstadoNovedad;
}

/**
 * Un traslado en el informe de cumplimiento.
 *
 * EL AGREGADO CONTESTA «CÓMO VAMOS»; ESTE CONTESTA «CUÁL FALLÓ». Un 66 % de
 * cumplimiento de plazo no dice qué traslado llegó tarde, con qué
 * transportadora ni con qué guía, y esos tres datos son los que hacen falta
 * para reclamar.
 */
export interface CumplimientoTrasladoDto {
  id: number;
  estado: EstadoTransferencia | null;
  productoNombre: string;
  sucursalOrigenId: number;
  sucursalOrigenNombre: string;
  sucursalDestinoId: number;
  sucursalDestinoNombre: string;
  transportadoraNombre: string | null;
  /** Con lo que se reclama. En un informe de cumplimiento no es un adorno. */
  guia: string | null;
  /** Unidad del traslado: la de las tres cantidades. */
  unidadSimbolo: string;
  /** Unidad base del producto: la de las cantidades de `lotes`. */
  unidadBaseSimbolo: string | null;
  cantidadSolicitada: number | null;
  cantidadDespachada: number | null;
  cantidadRecibida: number | null;
  fechaSolicitud: string | null;
  fechaDespacho: string | null;
  fechaEstimadaLlegada: string | null;
  fechaRecepcion: string | null;
  /** Días reales entre despacho y recepción. Nulo si falta una de las dos fechas. */
  diasTransito: number | null;
  /** Días de diferencia contra lo previsto: POSITIVO es tarde. Nulo sin comparación. */
  diasDesviacion: number | null;
  plazo: EstadoPlazo;
  /** Si llegó todo lo DESPACHADO. Nulo mientras no se reciba. */
  llegoCompleto: boolean | null;
  /** Si el origen mandó menos de lo pedido. No es una pérdida: nunca salió. */
  ajustadoEnOrigen: boolean;
  lotes: LoteTrasladadoDto[];
  novedades: NovedadResumenDto[];
  novedadesAbiertas: number;
}

export interface CumplimientoDetalleDto {
  desde: string | null;
  hasta: string | null;
  sucursalId: number | null;
  /** Del más reciente al más antiguo: es el orden en que se buscan las cosas. */
  traslados: CumplimientoTrasladoDto[];
  limite: number;
  /** Si el tope dejó traslados fuera. Para ver más allá se acota el periodo. */
  hayMas: boolean;
}
