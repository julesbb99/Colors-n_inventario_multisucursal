import { useMemo, useState } from 'react';
import { ArrowRight, BarChart3, Plus, Truck } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerTransferencias } from '../../services/transferencias';
import {
  ETIQUETA_ESTADO_TRANSFERENCIA,
  ETIQUETA_TIPO_NOVEDAD,
  ETIQUETA_TRATAMIENTO,
} from '../../models/transferencias';
import type { EstadoTransferencia, TransferenciaDto } from '../../models/transferencias';
import { DataTable } from '../../components/ui/DataTable';
import type { ColumnaTabla } from '../../components/ui/DataTable';
import { StatusBadge } from '../../components/ui/StatusBadge';
import type { EstadoBadge } from '../../components/ui/StatusBadge';
import { ChipColor } from '../../components/ui/ChipColor';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';
import { FormularioSolicitud } from '../../components/transferencias/FormularioSolicitud';
import { GestionTransportadoras } from '../../components/transferencias/GestionTransportadoras';
import { ReporteCumplimiento } from '../../components/transferencias/ReporteCumplimiento';
import { AccionesTraslado } from '../../components/transferencias/AccionesTraslado';
import type { AccionTraslado } from '../../components/transferencias/AccionesTraslado';
import { formatearFechaSolo, formatearVolumen } from '../../utils/formato';

/**
 * El estado del traslado traducido a tono.
 *
 * 'EnTransito' va en ámbar y no en gris a propósito: es mercancía que ya salió
 * del origen y todavía no suma en el destino, o sea que durante ese rato no está
 * en el saldo de nadie. Es el estado que hay que vigilar.
 */
const ESTADO_BADGE: Record<EstadoTransferencia, EstadoBadge> = {
  Solicitada: 'neutral',
  EnTransito: 'advertencia',
  RecibidaParcial: 'advertencia',
  Completada: 'exitoso',
  // Terminada, pero NO en verde: faltó mercancía y el tono no debería felicitar
  // por ello. Gris de cerrado, como las demás que acaban sin llegar entero.
  Cerrada: 'inactivo',
  Rechazada: 'critico',
  Cancelada: 'inactivo',
};

/**
 * Un traslado abierto todavía espera una acción de alguna de las dos sedes.
 *
 * 'RecibidaParcial' sigue aquí porque le queda un paso: dar cuenta del
 * faltante, y si ese faltante se reenvía o se reclama, esperar el desenlace.
 */
const ABIERTOS: readonly EstadoTransferencia[] = ['Solicitada', 'EnTransito', 'RecibidaParcial'];

/**
 * El filtro de la lista.
 *
 * MEZCLA GRUPOS Y ESTADOS SUELTOS a propósito, y eso no es un descuido de
 * diseño: las dos preguntas se hacen de verdad. «¿Qué tengo en curso?» es un
 * grupo; «¿cuáles se cancelaron?» es un estado concreto. Obligar a elegir una
 * sola forma dejaría media pantalla sin contestar.
 *
 * 'pendientes' no es ninguna de las dos cosas: es la pregunta nueva que trae el
 * tratamiento de novedades —«¿qué estoy esperando de una transportadora o de
 * otra sede?»— y no se puede expresar con un estado, porque todos esos
 * traslados están en 'RecibidaParcial'.
 */
type FiltroEstado =
  | 'todos'
  | 'enproceso'
  | 'cerrados'
  | 'pendientes'
  | EstadoTransferencia;

const OPCIONES_FILTRO: { id: FiltroEstado; etiqueta: string }[] = [
  { id: 'todos', etiqueta: 'Todas' },
  { id: 'enproceso', etiqueta: 'En proceso' },
  { id: 'pendientes', etiqueta: 'Con novedad pendiente' },
  { id: 'cerrados', etiqueta: 'Cerradas' },
  { id: 'Solicitada', etiqueta: 'Solicitadas' },
  { id: 'EnTransito', etiqueta: 'En tránsito' },
  { id: 'RecibidaParcial', etiqueta: 'Recibidas parciales' },
  { id: 'Completada', etiqueta: 'Completadas' },
  { id: 'Cerrada', etiqueta: 'Cerradas con faltante' },
  { id: 'Rechazada', etiqueta: 'Rechazadas' },
  { id: 'Cancelada', etiqueta: 'Canceladas' },
];

function cumpleFiltro(traslado: TransferenciaDto, filtro: FiltroEstado): boolean {
  if (filtro === 'todos') {
    return true;
  }
  if (filtro === 'pendientes') {
    return traslado.novedadesAbiertas > 0;
  }
  if (filtro === 'enproceso') {
    return traslado.estado !== null && ABIERTOS.includes(traslado.estado);
  }
  if (filtro === 'cerrados') {
    return traslado.estado === null || !ABIERTOS.includes(traslado.estado);
  }
  return traslado.estado === filtro;
}

const SIN_DATOS: TransferenciaDto[] = [];

/** Quién está mirando la tabla. Decide qué acciones se le ofrecen. */
interface QuienMira {
  usuarioId: number | null;
  /** Sede del token. `null` en el Administrador General: significa TODAS. */
  sedePropia: number | null;
  esAdminGeneral: boolean;
}

/**
 * Hoy, a medianoche local. Para comparar contra una fecha de llegada.
 *
 * Se compara por DÍA y no por instante: la fecha estimada se guarda a
 * medianoche, así que comparar instantes dejaría el botón oculto durante todo
 * el día de la llegada.
 */
function hoyAMedianoche(): number {
  const ahora = new Date();
  return new Date(ahora.getFullYear(), ahora.getMonth(), ahora.getDate()).getTime();
}

/** La fecha `AAAA-MM-DD…` de la API como medianoche LOCAL, sin pasar por UTC. */
function comoDiaLocal(iso: string): number | null {
  const partes = iso.slice(0, 10).split('-').map(Number);
  if (partes.length !== 3 || partes.some((n) => !Number.isFinite(n))) {
    return null;
  }
  return new Date(partes[0], partes[1] - 1, partes[2]).getTime();
}

/**
 * Si el traslado todavía no puede recibirse porque no ha llegado el día.
 *
 * Devuelve la fecha prevista cuando falta, y `null` cuando ya se puede.
 *
 * VALE PARA TODOS LOS ROLES, incluido el Administrador General: quien cuenta la
 * mercancía es quien la tiene delante, y el rango no adelanta el camión. La API
 * responde 409 igualmente; esto solo evita ofrecer un botón que va a fallar.
 */
function esperaLlegada(traslado: TransferenciaDto): string | null {
  if (traslado.fechaEstimadaLlegada === null) {
    return null;
  }
  const llegada = comoDiaLocal(traslado.fechaEstimadaLlegada);
  if (llegada === null) {
    return null;
  }
  return llegada > hoyAMedianoche() ? traslado.fechaEstimadaLlegada : null;
}

/**
 * Qué acciones admite un traslado según su estado Y QUIÉN ESTÁ MIRANDO.
 *
 * CADA ACCIÓN MIRA UN LADO DISTINTO, y es la misma regla que ya aplica la API:
 *
 *   despachar      el ORIGEN: el stock sale de su bodega
 *   rechazar       el ORIGEN: es quien decide no atender la petición
 *   recibir        el DESTINO, y no antes del día de llegada
 *   cancelar       QUIEN LA PIDIÓ: es su petición y aún no se ha movido nada
 *   novedad        el DESTINO: es quien tiene delante lo que llegó corto
 *   cerrar novedad cualquiera de las dos: el reenvío lo resuelve el origen y la
 *                  reclamación quien lleve la logística
 *
 * El Administrador General no tiene sede propia y las alcanza todas.
 */
function accionesDisponibles(
  traslado: TransferenciaDto,
  quien: QuienMira,
): AccionTraslado[] {
  const suSede = (sucursalId: number) =>
    quien.esAdminGeneral || sucursalId === quien.sedePropia;

  const enElOrigen = suSede(traslado.sucursalOrigenId);
  const enElDestino = suSede(traslado.sucursalDestinoId);
  // Cancelar es retirar UNA PETICIÓN PROPIA, no cerrar el documento de otro. Por
  // eso va por persona y no por sede: en una misma bodega, quien pidió el
  // traslado es quien sabe si ya no hace falta.
  const laPidioQuienMira =
    quien.usuarioId !== null && traslado.usuarioId === quien.usuarioId;

  if (traslado.estado === 'Solicitada') {
    const acciones: AccionTraslado[] = [];
    if (enElOrigen) {
      // Aceptarla es despacharla. Rechazarla es decir que no se atiende. Las
      // dos son del origen y las dos las puede hacer quien atiende la bodega.
      acciones.push('despacho', 'rechazo');
    }
    if (laPidioQuienMira || quien.esAdminGeneral) {
      acciones.push('cancelacion');
    }
    return acciones;
  }

  // UN SOLO BOTÓN, Y SOLO PARA EL DESTINO. Lo que faltó o llegó dañado se anota
  // DENTRO de la recepción, en el mismo paso en que se cuenta lo que llegó.
  //
  // NO ANTES DEL DÍA DE LLEGADA: mientras el camión viaja no hay nada que
  // contar, y la columna lo dice con la fecha en vez de dejar el hueco vacío.
  if (traslado.estado === 'EnTransito') {
    return enElDestino && esperaLlegada(traslado) === null ? ['recepcion'] : [];
  }

  // Llegó corto. Dos situaciones distintas y un botón para cada una:
  //
  //   sin novedad abierta   falta decir qué pasó con el faltante
  //   con novedad abierta   ya se dijo, y se está esperando el desenlace: lo
  //                         que queda es cerrarla cuando se resuelva
  if (traslado.estado === 'RecibidaParcial') {
    if (traslado.novedadesAbiertas > 0) {
      return enElOrigen || enElDestino ? ['cierreNovedad'] : [];
    }
    return enElDestino ? ['novedad'] : [];
  }

  // Completada, Cerrada, Rechazada y Cancelada no admiten nada más.
  return [];
}

const ETIQUETA_ACCION: Record<AccionTraslado, string> = {
  despacho: 'Despachar',
  recepcion: 'Recibir',
  // «Novedad de traslado» y NO «Recibir».
  //
  // Llamarlo «Recibir» hacía creer que quedaba algo por recibir, y no queda: la
  // mercancía ya entró, el traslado cerró corto y esto solo deja constancia de
  // qué pasó con el faltante. Recibir se hace UNA vez, y es el botón de arriba.
  novedad: 'Novedad de traslado',
  cierreNovedad: 'Cerrar novedad',
  rechazo: 'Rechazar',
  cancelacion: 'Cancelar',
};

function construirColumnas(
  quien: QuienMira,
  onAccion: (traslado: TransferenciaDto, accion: AccionTraslado) => void,
): ColumnaTabla<TransferenciaDto>[] {
  return [
    {
      id: 'numero',
      header: 'Nº',
      render: (t) => <span className="font-semibold tabular-nums text-slate-900">{t.id}</span>,
    },
    {
      id: 'ruta',
      header: 'Ruta',
      render: (t) => (
        <span className="flex items-center gap-2 text-slate-700">
          <span className="truncate">{t.sucursalOrigenNombre}</span>
          <ArrowRight size={15} className="shrink-0 text-terracota-600" aria-hidden="true" />
          <span className="truncate">{t.sucursalDestinoNombre}</span>
        </span>
      ),
    },
    {
      id: 'producto',
      header: 'Producto y lotes',
      render: (t) => (
        <span className="flex items-start gap-2.5">
          <ChipColor nombre={t.productoNombre} alto={26} />
          <span className="min-w-0">
            <span className="block text-slate-700">{t.productoNombre}</span>

            {/*
              LOS NÚMEROS DE LOTE, debajo del producto.

              El nombre del producto no distingue una tanda de otra: «Pintura
              Epóxica» son todas, y lo que hay que cotejar contra el envase que
              baja del camión es el número impreso. Con él además se sabe qué
              vence, que es lo que ordena la cola FEFO.

              Vacío mientras el traslado está Solicitada: hasta el despacho no
              ha salido nada, así que no hay lote que nombrar todavía. Se
              muestra un guion y no un hueco, para que no se lea como un dato
              que falta.
            */}
            {t.lotes.length === 0 ? (
              <span className="block text-xs text-slate-400">
                {t.estado === 'Solicitada' ? 'sin despachar' : '—'}
              </span>
            ) : (
              <span className="mt-0.5 flex flex-wrap gap-1">
                {t.lotes.map((l, i) => (
                  <span
                    key={l.loteId ?? `sin-lote-${i}`}
                    // OJO CON LA UNIDAD: la cantidad del lote va en la unidad
                    // BASE del producto, no en la del traslado. Un traslado de
                    // 5 galones mueve 18,93 litros de lote; rotularlo con
                    // `unidadSimbolo` diría «18,93 gal».
                    title={
                      `${formatearVolumen(l.cantidadBase, t.unidadBaseSimbolo)} de este lote` +
                      (l.fechaVencimiento === null
                        ? ' · sin caducidad'
                        : ` · vence el ${formatearFechaSolo(l.fechaVencimiento)}`)
                    }
                    className={`inline-flex items-center whitespace-nowrap rounded px-1.5 py-0.5 font-mono text-[11px] ${
                      // Sin lote asignado no es lo mismo que un lote: esa
                      // cantidad viaja sin trazabilidad del fabricante, y
                      // conviene que se note en vez de disimularlo.
                      l.numeroLote === null
                        ? 'bg-amber-50 text-amber-800 ring-1 ring-inset ring-amber-200'
                        : 'bg-slate-100 text-slate-600'
                    }`}
                  >
                    {l.numeroLote ?? 'sin lote'}
                  </span>
                ))}
              </span>
            )}
          </span>
        </span>
      ),
    },
    {
      id: 'cantidad',
      header: 'Pedido',
      align: 'derecha',
      // `whitespace-nowrap` en las tres columnas de cantidad: sin esto, en una
      // ventana estrecha «10,0 L» se parte entre el número y la unidad y queda
      // un «10,0» suelto sobre una «L», que se lee como otra cifra.
      render: (t) => (
        <span className="whitespace-nowrap font-semibold tabular-nums text-slate-900">
          {formatearVolumen(t.cantidadSolicitada, t.unidadSimbolo)}
        </span>
      ),
    },
    {
      id: 'despachado',
      header: 'Despachado',
      align: 'derecha',
      render: (t) => {
        // Menos de lo pedido NO es una pérdida: el origen no tenía todo y esa
        // mercancía sigue en su estante. Se marca en ámbar -no en el rojo del
        // faltante- justo para que no se lea como una merma.
        const ajustado =
          t.cantidadDespachada !== null &&
          t.cantidadSolicitada !== null &&
          t.cantidadDespachada < t.cantidadSolicitada;

        return (
          <span
            className={`whitespace-nowrap tabular-nums ${ajustado ? 'font-semibold text-amber-700' : 'text-slate-600'}`}
            title={ajustado ? 'El origen ajustó el envío: no tenía todo lo pedido.' : undefined}
          >
            {formatearVolumen(t.cantidadDespachada, t.unidadSimbolo)}
          </span>
        );
      },
    },
    {
      id: 'recibido',
      header: 'Recibido',
      align: 'derecha',
      render: (t) => {
        // Nulo mientras no se reciba: "—" dice "todavía no", y un 0 diría
        // "llegó vacío", que es un hecho distinto.
        //
        // Se compara contra lo DESPACHADO: si el origen mandó 3 de 5 y llegaron
        // 3, no falta nada y no hay por qué pintarlo en rojo.
        const referencia = t.cantidadDespachada ?? t.cantidadSolicitada;
        const falta =
          t.cantidadRecibida !== null &&
          referencia !== null &&
          t.cantidadRecibida < referencia;

        return (
          <span
            className={`whitespace-nowrap tabular-nums ${falta ? 'font-semibold text-terracota-700' : 'text-slate-600'}`}
          >
            {formatearVolumen(t.cantidadRecibida, t.unidadSimbolo)}
          </span>
        );
      },
    },
    {
      id: 'fecha',
      header: 'Fecha',
      align: 'derecha',
      render: (t) => (
        <span className="tabular-nums text-slate-600">{formatearFechaSolo(t.fechaSolicitud)}</span>
      ),
    },
    {
      id: 'novedades',
      header: 'Novedad',
      ancho: 'w-44',
      render: (t) => {
        if (t.novedades.length === 0) {
          return <span className="text-slate-400">—</span>;
        }

        // UNA SOLA FILA, SIEMPRE.
        //
        // Antes se apilaban dos y un «+N más» debajo, y en un traslado con
        // cuatro eso llenaba la celda de historia vieja: la fila crecía al
        // triple y ninguna de las cuatro destacaba sobre las demás. En una
        // tabla, esta columna contesta una pregunta -«¿este traslado tiene
        // algo?»- y esa se contesta con un renglón.
        //
        // CUÁL SE ELIGE: la abierta si la hay, porque es la única sobre la que
        // queda algo que hacer; si ninguna lo está, la más reciente, que es lo
        // último que se supo del traslado. `novedades` llega ordenada de más
        // nueva a más vieja desde la API.
        const principal = t.novedades.find((n) => n.estado === 'Abierta') ?? t.novedades[0];
        const otras = t.novedades.length - 1;
        const abierta = principal.estado === 'Abierta';

        const describir = (n: TransferenciaDto['novedades'][number]) =>
          `${n.tipo === null ? 'Novedad' : ETIQUETA_TIPO_NOVEDAD[n.tipo]}` +
          `${n.cantidadAfectada === null ? '' : ` ${formatearVolumen(n.cantidadAfectada, t.unidadSimbolo)}`}` +
          ` · ${formatearFechaSolo(n.fecha)} · ${
            n.estado === 'Abierta'
              ? `pendiente (${ETIQUETA_TRATAMIENTO[n.tratamiento]})`
              : n.motivoCierre ?? 'cerrada'
          }`;

        return (
          <span className="flex items-center gap-1.5">
            <span
              // El tooltip SÍ las lleva todas: la celda muestra una, pero el
              // dato no se pierde. Sin esto, colapsar a una sería esconderlas.
              title={t.novedades.map(describir).join('\n')}
              className={`inline-flex w-fit items-center gap-1.5 whitespace-nowrap rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                abierta
                  ? 'bg-terracota-100 text-terracota-800 ring-1 ring-inset ring-terracota-300'
                  : 'bg-slate-100 text-slate-600'
              }`}
            >
              {/* El punto marca lo que sigue esperando desenlace: es lo que
                  distingue un pendiente de un apunte ya resuelto. */}
              {abierta ? (
                <span
                  aria-hidden="true"
                  className="h-1.5 w-1.5 shrink-0 rounded-full bg-terracota-600"
                />
              ) : null}
              {principal.tipo === null ? 'Novedad' : ETIQUETA_TIPO_NOVEDAD[principal.tipo]}
              {principal.cantidadAfectada === null
                ? ''
                : ` ${formatearVolumen(principal.cantidadAfectada, t.unidadSimbolo)}`}
            </span>

            {/* El resto, como un número y no como más renglones. Que existan
                hay que decirlo -esconderlas sería mentir sobre el historial-
                pero no compiten con la que importa. */}
            {otras > 0 ? (
              <span
                title={`Este traslado tiene ${t.novedades.length} novedades en total. Pasa el cursor por la etiqueta para verlas.`}
                className="shrink-0 cursor-default text-[11px] font-semibold tabular-nums text-slate-400"
              >
                +{otras}
              </span>
            ) : null}
          </span>
        );
      },
    },
    {
      id: 'estado',
      header: 'Estado',
      align: 'centro',
      render: (t) =>
        t.estado === null ? (
          <StatusBadge estado="neutral" />
        ) : (
          <span className="flex flex-col items-center gap-1">
            <StatusBadge estado={ESTADO_BADGE[t.estado]}>
              {ETIQUETA_ESTADO_TRANSFERENCIA[t.estado]}
            </StatusBadge>
            {/* Un traslado recibido parcial con algo pendiente no está
                terminado, y la etiqueta sola no lo dice. */}
            {t.novedadesAbiertas > 0 ? (
              <span className="text-[10px] font-semibold uppercase tracking-wide text-terracota-700">
                por resolver
              </span>
            ) : null}
          </span>
        ),
    },
    {
      id: 'acciones',
      header: '',
      align: 'derecha',
      // Ancho reservado para la columna. Sin esto la tabla se lo reparte entre
      // todas y deja aquí lo que sobre, que con «Novedad de traslado» eran unos
      // 60 px: el rótulo se partía en tres líneas y se salía del botón.
      ancho: 'w-56',
      render: (traslado) => {
        const acciones = accionesDisponibles(traslado, quien);

        if (acciones.length === 0) {
          // SI LO QUE FALTA ES EL DÍA, se dice. Un hueco vacío en un traslado
          // que va llegando se lee como «esto no es tuyo», y sí lo es: solo que
          // todavía no.
          const espera = traslado.estado === 'EnTransito' ? esperaLlegada(traslado) : null;

          if (espera !== null) {
            return (
              <span className="whitespace-nowrap text-xs text-slate-500">
                Llega el {formatearFechaSolo(espera)}
              </span>
            );
          }

          return <span className="text-slate-400">—</span>;
        }

        return (
          <span className="flex flex-wrap justify-end gap-1.5">
            {acciones.map((accion) => (
              <button
                key={accion}
                type="button"
                onClick={() => onAccion(traslado, accion)}
                // `whitespace-nowrap` es lo que impide que el rótulo se parta
                // dentro de un botón de altura fija. Si alguna vez no cabe, lo que
                // se rompe es la línea ENTRE botones -de ahí el flex-wrap de
                // arriba- y no el texto de uno.
                className="inline-flex h-9 shrink-0 items-center whitespace-nowrap rounded-lg border border-slate-300 bg-white px-2.5 text-xs font-semibold text-slate-700 transition hover:border-terracota-400 hover:text-terracota-700"
              >
                {ETIQUETA_ACCION[accion]}
              </button>
            ))}
          </span>
        );
      },
    },
  ];
}

interface Pendiente {
  traslado: TransferenciaDto;
  accion: AccionTraslado;
}

export function Traslados() {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const { sesion, sucursalId: sedePropia, esAdminGeneral, esSupervision } = useAuth();
  const [filtro, setFiltro] = useState<FiltroEstado>('todos');
  const [solicitando, setSolicitando] = useState(false);
  const [transportadorasAbierto, setTransportadorasAbierto] = useState(false);
  const [reporteAbierto, setReporteAbierto] = useState(false);
  const [pendiente, setPendiente] = useState<Pendiente | null>(null);

  const {
    datos: traslados,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(() => obtenerTransferencias(sedeActiva), [sedeActiva], SIN_DATOS);

  const quien = useMemo<QuienMira>(
    () => ({ usuarioId: sesion?.usuarioId ?? null, sedePropia, esAdminGeneral }),
    [sesion?.usuarioId, sedePropia, esAdminGeneral],
  );

  const columnas = useMemo(
    () =>
      construirColumnas(quien, (traslado, accion) =>
        setPendiente({ traslado, accion }),
      ),
    [quien],
  );

  /**
   * Cuántos caen en cada opción del filtro.
   *
   * Van EN LAS ETIQUETAS del selector y no en pestañas aparte: con once
   * opciones, once pestañas no caben, y el número es justo lo que permite
   * elegir sin probar una por una.
   */
  const conteos = useMemo(() => {
    const mapa = {} as Record<FiltroEstado, number>;
    for (const opcion of OPCIONES_FILTRO) {
      mapa[opcion.id] = traslados.filter((t) => cumpleFiltro(t, opcion.id)).length;
    }
    return mapa;
  }, [traslados]);

  const filtrados = useMemo(
    () => traslados.filter((t) => cumpleFiltro(t, filtro)),
    [traslados, filtro],
  );

  const pendientesDeResolver = useMemo(
    () => traslados.reduce((suma, t) => suma + t.novedadesAbiertas, 0),
    [traslados],
  );

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-0 flex-col gap-1.5">
          <label htmlFor="filtro-estado" className="text-sm font-semibold text-slate-700">
            Estado
          </label>
          <select
            id="filtro-estado"
            value={filtro}
            onChange={(evento) => setFiltro(evento.target.value as FiltroEstado)}
            className="h-11 w-full cursor-pointer rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 outline-none transition focus:border-petroleo-500 sm:w-72"
          >
            {OPCIONES_FILTRO.map(({ id, etiqueta }) => (
              <option key={id} value={id}>
                {etiqueta} ({conteos[id] ?? 0})
              </option>
            ))}
          </select>
        </div>

        {/* Empujador, no texto. La frase de contexto va en su propia línea
            (debajo): metida aquí, el `flex-1` le daba el hueco que sobrara
            entre el selector y los botones, que en una ventana estrecha eran
            80 px y la partía en seis renglones. */}
        <span className="flex-1" aria-hidden="true" />

        <Boton variante="secundaria" onClick={() => setReporteAbierto(true)}>
          <BarChart3 size={17} aria-hidden="true" />
          Cumplimiento
        </Boton>

        {/* El catálogo es de toda la red, pero lo mantienen administración y
            gerencia: quien negocia el flete de su sede sabe con quién se
            trabaja. La API responde 403 al operario. */}
        {esSupervision ? (
          <Boton variante="secundaria" onClick={() => setTransportadorasAbierto(true)}>
            <Truck size={17} aria-hidden="true" />
            Transportadoras
          </Boton>
        ) : null}

        {/* Pedir mercancía es del día a día: cualquier rol puede. Lo que exige
            supervisión es cerrar el documento (rechazo, cancelación). */}
        <Boton onClick={() => setSolicitando(true)}>
          <Plus size={17} aria-hidden="true" />
          Solicitar traslado
        </Boton>
      </div>

      <p className="text-sm text-slate-500">
        {nombreSedeActiva} · lo que la sede envía y lo que recibe
      </p>

      {/* El aviso va arriba y solo cuando hay algo: un reenvío o una reclamación
          sin resolver es trabajo que alguien tiene que ir a cobrar, y si solo
          se ve entrando a filtrar, se olvida. */}
      {pendientesDeResolver > 0 && filtro !== 'pendientes' ? (
        <Alerta tipo="info">
          Hay {pendientesDeResolver}{' '}
          {pendientesDeResolver === 1 ? 'novedad pendiente' : 'novedades pendientes'} de desenlace
          —reenvíos o reclamaciones sin cerrar—. Esos traslados siguen en «por recibir».{' '}
          <button
            type="button"
            onClick={() => setFiltro('pendientes')}
            className="font-semibold underline underline-offset-2"
          >
            Verlos
          </button>
        </Alerta>
      ) : null}

      {solicitando ? (
        <FormularioSolicitud onCerrar={() => setSolicitando(false)} onSolicitada={recargar} />
      ) : null}

      {transportadorasAbierto ? (
        <GestionTransportadoras
          onCerrar={() => setTransportadorasAbierto(false)}
          // Recarga la tabla: un traslado no cambia, pero el formulario de
          // despacho vuelve a pedir el catálogo al abrirse y así ve el plazo
          // nuevo sin recargar la página.
          onGuardado={recargar}
        />
      ) : null}

      {reporteAbierto ? <ReporteCumplimiento onCerrar={() => setReporteAbierto(false)} /> : null}

      {pendiente ? (
        <AccionesTraslado
          traslado={pendiente.traslado}
          accion={pendiente.accion}
          onCerrar={() => setPendiente(null)}
          onHecho={recargar}
        />
      ) : null}

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <DataTable
          columnas={columnas}
          data={filtrados}
          claveFila={(t) => t.id}
          cargando={cargando}
          estadoVacio={
            filtro === 'todos'
              ? 'No hay traslados registrados. Los solicita la sede que necesita la mercancía.'
              : 'Ningún traslado en ese estado.'
          }
        />
      )}
    </div>
  );
}
