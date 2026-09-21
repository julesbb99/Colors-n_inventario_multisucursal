import { useMemo, useState } from 'react';
import { ArrowRight, Plus } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerTransferencias } from '../../services/transferencias';
import { ETIQUETA_ESTADO_TRANSFERENCIA } from '../../models/transferencias';
import type { EstadoTransferencia, TransferenciaDto } from '../../models/transferencias';
import { DataTable } from '../../components/ui/DataTable';
import type { ColumnaTabla } from '../../components/ui/DataTable';
import { StatusBadge } from '../../components/ui/StatusBadge';
import type { EstadoBadge } from '../../components/ui/StatusBadge';
import { ChipColor } from '../../components/ui/ChipColor';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';
import { FormularioSolicitud } from '../../components/transferencias/FormularioSolicitud';
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
  Rechazada: 'critico',
  Cancelada: 'inactivo',
};

type Pestana = 'todos' | 'abiertos' | 'cerrados';

const PESTANAS: { id: Pestana; etiqueta: string }[] = [
  { id: 'todos', etiqueta: 'Todos' },
  { id: 'abiertos', etiqueta: 'En curso' },
  { id: 'cerrados', etiqueta: 'Cerrados' },
];

/** Un traslado abierto todavía espera una acción de alguna de las dos sedes. */
const ABIERTOS: readonly EstadoTransferencia[] = ['Solicitada', 'EnTransito', 'RecibidaParcial'];

const SIN_DATOS: TransferenciaDto[] = [];

/** Quién está mirando la tabla. Decide qué acciones se le ofrecen. */
interface QuienMira {
  usuarioId: number | null;
  /** Sede del token. `null` en el Administrador General: significa TODAS. */
  sedePropia: number | null;
  esAdminGeneral: boolean;
}

/**
 * Qué acciones admite un traslado según su estado Y QUIÉN ESTÁ MIRANDO.
 *
 * ANTES SOLO MIRABA EL ESTADO Y EL ROL, y ese era el fallo: a un traslado
 * Solicitada se le ofrecía «Despachar» a cualquiera, así que la sede DESTINO
 * -la que pidió el producto- veía el botón de despachar mercancía que no tiene.
 * La API lo rechazaba con 403, pero ofrecer un botón que va a fallar es peor que
 * no ofrecerlo: parece que la acción es tuya y no lo es.
 *
 * CADA ACCIÓN MIRA UN LADO DISTINTO, y es la misma regla que ya aplica la API:
 *
 *   despachar   el ORIGEN: el stock sale de su bodega
 *   rechazar    el ORIGEN: es quien decide no atender la petición
 *   recibir     el DESTINO: es donde entra la mercancía
 *   cancelar    QUIEN LA PIDIÓ: es su petición y aún no se ha movido nada
 *   novedad     cualquiera de las dos: el daño se ve al cargar o al descargar
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

  // UN SOLO BOTÓN, Y SOLO PARA EL DESTINO.
  //
  // Antes había dos -«Recibir» y «Novedad»- y los veían las dos sedes. Sobraba
  // uno: lo que faltó o llegó dañado se anota DENTRO de la recepción, en el
  // mismo paso en que se cuenta lo que llegó, así que un botón aparte invitaba
  // a hacerlo en dos sitios. Y el origen no tiene nada que anotar: no ve la
  // mercancía llegar.
  if (traslado.estado === 'EnTransito') {
    return enElDestino ? ['recepcion'] : [];
  }

  // Ya se recibió, pero llegó corto. Queda dejar constancia de lo que pasó con
  // el faltante: si apareció, si lo respondió la transportadora, si se da por
  // perdido. Sigue siendo cosa del destino, que es quien lo tiene delante.
  if (traslado.estado === 'RecibidaParcial') {
    return enElDestino ? ['novedad'] : [];
  }

  // Completada, Rechazada y Cancelada están cerradas: no admiten nada más.
  return [];
}

const ETIQUETA_ACCION: Record<AccionTraslado, string> = {
  despacho: 'Despachar',
  recepcion: 'Recibir',
  // «Recibir» y no «Novedad»: en la bodega esto no es reportar una incidencia,
  // es terminar de revisar lo que llegó. Nunca convive con el de arriba porque
  // los dos estados son excluyentes.
  novedad: 'Recibir',
  rechazo: 'Rechazar',
  cancelacion: 'Cancelar',
};

function construirColumnas(
  quien: QuienMira,
  onAccion: (traslado: TransferenciaDto, accion: AccionTraslado) => void,
): ColumnaTabla<TransferenciaDto>[] {
  const columnas: ColumnaTabla<TransferenciaDto>[] = [
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
    header: 'Producto',
    render: (t) => (
      <span className="flex items-center gap-2.5">
        <ChipColor nombre={t.productoNombre} alto={26} />
        <span className="text-slate-700">{t.productoNombre}</span>
      </span>
    ),
  },
  {
    id: 'cantidad',
    header: 'Solicitado',
    align: 'derecha',
    render: (t) => (
      <span className="font-semibold tabular-nums text-slate-900">
        {formatearVolumen(t.cantidadSolicitada, t.unidadSimbolo)}
      </span>
    ),
  },
  {
    id: 'recibido',
    header: 'Recibido',
    align: 'derecha',
    render: (t) => {
      // Nulo mientras no se reciba: "—" dice "todavía no", y un 0 diría
      // "llegó vacío", que es un hecho distinto.
      const falta =
        t.cantidadRecibida !== null &&
        t.cantidadSolicitada !== null &&
        t.cantidadRecibida < t.cantidadSolicitada;

      return (
        <span
          className={`tabular-nums ${falta ? 'font-semibold text-terracota-700' : 'text-slate-600'}`}
        >
          {formatearVolumen(t.cantidadRecibida, t.unidadSimbolo)}
        </span>
      );
    },
  },
  { id: 'solicito', header: 'Solicitó', accessor: 'usuarioNombre' },
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
    header: 'Novedades',
    align: 'centro',
    render: (t) =>
      t.novedades.length === 0 ? (
        <span className="text-slate-400">—</span>
      ) : (
        <StatusBadge estado="advertencia">{t.novedades.length}</StatusBadge>
      ),
  },
  {
    id: 'estado',
    header: 'Estado',
    align: 'centro',
    render: (t) =>
      t.estado === null ? (
        <StatusBadge estado="neutral" />
      ) : (
        <StatusBadge estado={ESTADO_BADGE[t.estado]}>
          {ETIQUETA_ESTADO_TRANSFERENCIA[t.estado]}
        </StatusBadge>
      ),
  },
  {
    id: 'acciones',
    header: '',
    align: 'derecha',
    render: (traslado) => {
      const acciones = accionesDisponibles(traslado, quien);

      if (acciones.length === 0) {
        return <span className="text-slate-400">—</span>;
      }

      return (
        <span className="flex justify-end gap-1.5">
          {acciones.map((accion) => (
            <button
              key={accion}
              type="button"
              onClick={() => onAccion(traslado, accion)}
              className="inline-flex h-9 items-center rounded-lg border border-slate-300 bg-white px-2.5 text-xs font-semibold text-slate-700 transition hover:border-terracota-400 hover:text-terracota-700"
            >
              {ETIQUETA_ACCION[accion]}
            </button>
          ))}
        </span>
      );
    },
  },
  ];

  return columnas;
}

interface Pendiente {
  traslado: TransferenciaDto;
  accion: AccionTraslado;
}

export function Traslados() {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const { sesion, sucursalId: sedePropia, esAdminGeneral } = useAuth();
  const [pestana, setPestana] = useState<Pestana>('todos');
  const [solicitando, setSolicitando] = useState(false);
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

  const conteos = useMemo(
    () => ({
      todos: traslados.length,
      abiertos: traslados.filter((t) => t.estado !== null && ABIERTOS.includes(t.estado)).length,
      cerrados: traslados.filter((t) => t.estado === null || !ABIERTOS.includes(t.estado)).length,
    }),
    [traslados],
  );

  const filtrados = useMemo(() => {
    if (pestana === 'abiertos') {
      return traslados.filter((t) => t.estado !== null && ABIERTOS.includes(t.estado));
    }
    if (pestana === 'cerrados') {
      return traslados.filter((t) => t.estado === null || !ABIERTOS.includes(t.estado));
    }
    return traslados;
  }, [traslados, pestana]);

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-3 border-b border-slate-200">
        {PESTANAS.map(({ id, etiqueta }) => {
          const activa = id === pestana;

          return (
            <button
              key={id}
              type="button"
              onClick={() => setPestana(id)}
              aria-current={activa ? 'page' : undefined}
              className={`-mb-px flex h-11 items-center gap-2 border-b-2 px-1 text-sm transition ${
                activa
                  ? 'border-terracota-500 font-bold text-slate-900'
                  : 'border-transparent font-medium text-slate-500 hover:text-slate-800'
              }`}
            >
              {etiqueta}
              <span
                className={`inline-flex min-w-[22px] items-center justify-center rounded-full px-1.5 py-0.5 text-[11px] font-bold tabular-nums ${
                  activa ? 'bg-petroleo-800 text-white' : 'bg-slate-200 text-slate-600'
                }`}
              >
                {conteos[id]}
              </span>
            </button>
          );
        })}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <p className="min-w-0 flex-1 text-sm text-slate-500">
          {nombreSedeActiva} · lo que la sede envía y lo que recibe
        </p>

        {/* Pedir mercancía es del día a día: cualquier rol puede. Lo que exige
            supervisión es cerrar el documento (rechazo, cancelación). */}
        <Boton onClick={() => setSolicitando(true)}>
          <Plus size={17} aria-hidden="true" />
          Solicitar traslado
        </Boton>
      </div>

      {solicitando ? (
        <FormularioSolicitud onCerrar={() => setSolicitando(false)} onSolicitada={recargar} />
      ) : null}

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
          estadoVacio="No hay traslados registrados. Los solicita la sede que necesita la mercancía."
        />
      )}
    </div>
  );
}
