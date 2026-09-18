import axiosInstance from '../interceptors/axiosInstance';
import type {
  CierreTransferenciaDto,
  CrearTransferenciaDto,
  DespacharTransferenciaDto,
  RecibirTransferenciaDto,
  RegistrarNovedadDto,
  TransferenciaDto,
  TransportadoraDto,
} from '../models/transferencias';

/** La ruta es `/transferencias`. `/traslados` no existe: responde 404. */
const RUTA = '/transferencias';

export async function obtenerTransportadoras(): Promise<TransportadoraDto[]> {
  const { data } = await axiosInstance.get<TransportadoraDto[]>(`${RUTA}/transportadoras`);
  return data;
}

/**
 * Traslados de una sede, o de toda la red.
 *
 * POR QUE DOS LLAMADAS CUANDO HAY SEDE. Un traslado tiene dos sedes, y el
 * endpoint filtra por `sucursalOrigenId` y `sucursalDestinoId` POR SEPARADO:
 * mandar los dos con el mismo valor pediria traslados de una sede a si misma,
 * que no existen. Para ver los de una sede hay que preguntar dos veces -lo que
 * envia y lo que recibe- y unir.
 *
 * Filtrar en el cliente sobre la lista completa seria mas corto y estaria mal:
 * el servidor recorta a `limite` ANTES de devolver, asi que al quedarnos con una
 * sede se perderian traslados suyos que quedaron fuera de ese recorte.
 *
 * Sin sede -el administrador viendo "Todas"- basta una llamada.
 */
export async function obtenerTransferencias(
  sucursalId: number | null,
): Promise<TransferenciaDto[]> {
  if (sucursalId === null) {
    const { data } = await axiosInstance.get<TransferenciaDto[]>(RUTA);
    return data;
  }

  const [enviadas, recibidas] = await Promise.all([
    axiosInstance.get<TransferenciaDto[]>(RUTA, {
      params: { sucursalOrigenId: sucursalId },
    }),
    axiosInstance.get<TransferenciaDto[]>(RUTA, {
      params: { sucursalDestinoId: sucursalId },
    }),
  ]);

  const porId = new Map<number, TransferenciaDto>();
  for (const traslado of [...enviadas.data, ...recibidas.data]) {
    porId.set(traslado.id, traslado);
  }

  // Mas reciente primero, con el id de desempate: dos traslados solicitados en
  // el mismo segundo saldrian en orden arbitrario sin el.
  return [...porId.values()].sort((a, b) => {
    const fechaA = a.fechaSolicitud ?? '';
    const fechaB = b.fechaSolicitud ?? '';
    return fechaA === fechaB ? b.id - a.id : fechaA < fechaB ? 1 : -1;
  });
}

export async function obtenerTransferencia(id: number): Promise<TransferenciaDto> {
  const { data } = await axiosInstance.get<TransferenciaDto>(`${RUTA}/${id}`);
  return data;
}

/** La solicita la sede DESTINO. Nace 'Solicitada' y todavia no mueve stock. */
export async function solicitarTransferencia(peticion: CrearTransferenciaDto) {
  const { data } = await axiosInstance.post(RUTA, peticion);
  return data;
}

/** Lo hace la sede ORIGEN. Descuenta stock por FEFO y pasa a 'EnTransito'. */
export async function despacharTransferencia(id: number, peticion: DespacharTransferenciaDto) {
  const { data } = await axiosInstance.post(`${RUTA}/${id}/despacho`, peticion);
  return data;
}

/** Lo hace la sede DESTINO. Sin `cantidadRecibida` llega todo lo despachado. */
export async function recibirTransferencia(id: number, peticion: RecibirTransferenciaDto) {
  const { data } = await axiosInstance.post(`${RUTA}/${id}/recepcion`, peticion);
  return data;
}

/** Solo el ORIGEN y solo desde 'Solicitada'. Restringido a supervision. */
export async function rechazarTransferencia(id: number, peticion: CierreTransferenciaDto = {}) {
  const { data } = await axiosInstance.post(`${RUTA}/${id}/rechazo`, peticion);
  return data;
}

/** Cualquiera de las dos sedes, solo desde 'Solicitada'. Restringido a supervision. */
export async function cancelarTransferencia(id: number, peticion: CierreTransferenciaDto = {}) {
  const { data } = await axiosInstance.post(`${RUTA}/${id}/cancelacion`, peticion);
  return data;
}

/**
 * Reportar una novedad. No mueve stock.
 *
 * `Faltante` y `Averia` son criticas y solo las admite supervision: a un
 * operador la API le responde 403 aunque el endpoint sea el mismo.
 */
export async function registrarNovedad(id: number, peticion: RegistrarNovedadDto) {
  const { data } = await axiosInstance.post(`${RUTA}/${id}/novedades`, peticion);
  return data;
}
