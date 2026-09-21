import axiosInstance from '../interceptors/axiosInstance';
import { paramsDeSede } from './comun';
import type {
  CerrarNovedadDto,
  CierreTransferenciaDto,
  CrearTransferenciaDto,
  DespacharTransferenciaDto,
  GuardarTransportadoraDto,
  RecibirTransferenciaDto,
  RegistrarNovedadDto,
  ReporteCumplimientoDto,
  TransferenciaDto,
  TransportadoraDto,
} from '../models/transferencias';

/** La ruta es `/transferencias`. `/traslados` no existe: responde 404. */
const RUTA = '/transferencias';

/**
 * Catálogo de transportadoras.
 *
 * Por omisión SIN las retiradas, que es lo que quiere el selector del despacho.
 * La pantalla que las administra pide las dos para poder reactivarlas.
 */
export async function obtenerTransportadoras(
  incluirRetiradas = false,
): Promise<TransportadoraDto[]> {
  const { data } = await axiosInstance.get<TransportadoraDto[]>(`${RUTA}/transportadoras`, {
    params: incluirRetiradas ? { incluirRetiradas: true } : undefined,
  });
  return data;
}

/**
 * Da de alta una transportadora.
 *
 * Administración general y gerencia de sede. La API responde 403 al operario y
 * 409 si el nombre ya existe.
 */
export async function crearTransportadora(
  peticion: GuardarTransportadoraDto,
): Promise<TransportadoraDto> {
  const { data } = await axiosInstance.post<TransportadoraDto>(
    `${RUTA}/transportadoras`,
    peticion,
  );
  return data;
}

/**
 * Cambia los datos de una transportadora.
 *
 * NO toca los traslados ya despachados: cada uno guarda su guía y su fecha
 * estimada propias. Cambiar los días afecta a los despachos que vengan.
 */
export async function actualizarTransportadora(
  id: number,
  peticion: GuardarTransportadoraDto,
): Promise<TransportadoraDto> {
  const { data } = await axiosInstance.put<TransportadoraDto>(
    `${RUTA}/transportadoras/${id}`,
    peticion,
  );
  return data;
}

/**
 * Retira una transportadora del catálogo.
 *
 * NO LA BORRA, aunque el verbo HTTP sea DELETE: la fila se conserva y los
 * traslados que llevó la siguen citando. Deja de ofrecerse al despachar, y un
 * despacho que la pida igualmente recibe 409.
 */
export async function retirarTransportadora(id: number): Promise<TransportadoraDto> {
  const { data } = await axiosInstance.delete<TransportadoraDto>(`${RUTA}/transportadoras/${id}`);
  return data;
}

/** Devuelve al catálogo una retirada, conservando su historia. */
export async function reactivarTransportadora(id: number): Promise<TransportadoraDto> {
  const { data } = await axiosInstance.post<TransportadoraDto>(
    `${RUTA}/transportadoras/${id}/reactivar`,
  );
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

/**
 * El ORIGEN dice que no atiende la peticion. Solo desde 'Solicitada'.
 *
 * Cualquier rol de esa sede: aceptar -despachar- y rechazar son la misma
 * decision vista desde los dos lados, y quien mira el estante es quien sabe si
 * hay producto. No mueve stock.
 */
export async function rechazarTransferencia(id: number, peticion: CierreTransferenciaDto = {}) {
  const { data } = await axiosInstance.post(`${RUTA}/${id}/rechazo`, peticion);
  return data;
}

/**
 * Retira la peticion. Solo desde 'Solicitada'.
 *
 * La cancela QUIEN LA PIDIO, sea cual sea su rol: es su peticion y aun no se ha
 * movido nada. Otro usuario recibe 403 salvo que sea supervision.
 */
export async function cancelarTransferencia(id: number, peticion: CierreTransferenciaDto = {}) {
  const { data } = await axiosInstance.post(`${RUTA}/${id}/cancelacion`, peticion);
  return data;
}

/**
 * Reportar una novedad. NO MUEVE STOCK, y en el faltante eso es lo importante:
 * lo que salio del origen y nunca llego ya esta descontado de la red, asi que
 * esta anotacion lo deja registrado como merma sin restarlo por segunda vez.
 *
 * Abierta a cualquier rol de las dos sedes del traslado.
 */
export async function registrarNovedad(id: number, peticion: RegistrarNovedadDto) {
  const { data } = await axiosInstance.post(`${RUTA}/${id}/novedades`, peticion);
  return data;
}

/**
 * Cierra una novedad que quedó esperando desenlace: llegó lo que faltaba, la
 * transportadora respondió, o se da por perdido.
 *
 * NO BORRA NADA. La novedad se conserva entera -tipo, cantidad, quién la
 * reportó y cuándo- y se le añade el desenlace con su motivo. El motivo es
 * obligatorio: sin él la API responde 400, porque sin él cerrar sería
 * indistinguible de borrar.
 *
 * Si era la última pendiente, el traslado sale de «por recibir» y pasa a
 * «Cerrada con faltante».
 */
export async function cerrarNovedad(
  id: number,
  novedadId: number,
  peticion: CerrarNovedadDto,
) {
  const { data } = await axiosInstance.post(
    `${RUTA}/${id}/novedades/${novedadId}/cierre`,
    peticion,
  );
  return data;
}

/**
 * Informe de cumplimiento logístico, por sucursal y por ruta.
 *
 * `desde` y `hasta` van en `AAAA-MM-DDTHH:MM:SS` SIN HUSO y filtran por fecha de
 * solicitud. La API acota igualmente a la sede de quien pregunta.
 */
export async function obtenerReporteCumplimiento(
  sucursalId: number | null,
  desde?: string | null,
  hasta?: string | null,
): Promise<ReporteCumplimientoDto> {
  const { data } = await axiosInstance.get<ReporteCumplimientoDto>(
    `${RUTA}/reportes/cumplimiento`,
    {
      params: {
        ...paramsDeSede(sucursalId),
        ...(desde ? { desde } : {}),
        ...(hasta ? { hasta } : {}),
      },
    },
  );
  return data;
}
