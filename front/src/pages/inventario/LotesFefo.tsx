import { useMemo, useState } from 'react';
import { Plus } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerLotes } from '../../services/inventario';
import { estadoCaducidad } from '../../models/inventario';
import type { EstadoCaducidad, LoteDto } from '../../models/inventario';
import { DataTable } from '../../components/ui/DataTable';
import type { ColumnaTabla } from '../../components/ui/DataTable';
import { StatusBadge } from '../../components/ui/StatusBadge';
import type { EstadoBadge } from '../../components/ui/StatusBadge';
import { ChipColor } from '../../components/ui/ChipColor';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';
import { FormularioLote } from '../../components/inventario/FormularioLote';
import { formatearDias, formatearFechaSolo, formatearVolumen } from '../../utils/formato';

const ESTADO_BADGE: Record<EstadoCaducidad, EstadoBadge> = {
  Vigente: 'activo',
  'Por vencer': 'por_vencer',
  Vencido: 'vencido',
};

/**
 * Umbral con el que esta pantalla clasifica.
 *
 * El del servidor vive en `AlertasInventario:DiasUmbralVencimiento` y llega en
 * `ResumenGeneralDto.diasUmbralVencimiento`, pero ese DTO es del tablero y
 * pedirlo aqui solo para leer un numero seria una consulta entera de mas. El
 * valor coincide con el sembrado; si alla se cambia, hay que cambiarlo aqui.
 */
const DIAS_UMBRAL = 30;

type Pestana = 'todos' | 'porvencer' | 'vencidos' | 'sinfecha';

const PESTANAS: { id: Pestana; etiqueta: string }[] = [
  { id: 'todos', etiqueta: 'Todos' },
  { id: 'porvencer', etiqueta: 'Por vencer' },
  { id: 'vencidos', etiqueta: 'Vencidos' },
  { id: 'sinfecha', etiqueta: 'Sin caducidad' },
];

const VACIO_POR_PESTANA: Record<Pestana, string> = {
  todos: 'No hay lotes registrados para esta sede.',
  porvencer: 'Ningún lote con existencias caduca dentro del umbral.',
  vencidos:
    'Ningún lote vencido con existencias. Cuando aparezca uno sale de primero, porque ya no se puede despachar y hay que darlo de baja con un movimiento de ajuste.',
  sinfecha:
    'Todos los lotes tienen fecha. Un lote sin caducidad va al final de la cola FEFO y no entra en las alertas de vencimiento.',
};

const SIN_DATOS: LoteDto[] = [];

const COLUMNAS: ColumnaTabla<LoteDto>[] = [
  { id: 'lote', header: 'Lote', accessor: 'numeroLote' },
  {
    id: 'producto',
    header: 'Producto',
    render: (lote) => (
      <span className="flex items-center gap-2.5">
        <ChipColor nombre={lote.productoNombre} alto={26} />
        <span className="text-slate-700">{lote.productoNombre}</span>
      </span>
    ),
  },
  { id: 'sede', header: 'Sede', accessor: 'sucursalNombre' },
  {
    id: 'vence',
    header: 'Vence',
    align: 'derecha',
    render: (lote) => (
      <span className="tabular-nums text-slate-600">
        {formatearFechaSolo(lote.fechaVencimiento)}
      </span>
    ),
  },
  {
    id: 'dias',
    header: 'Días',
    align: 'derecha',
    render: (lote) => (
      <span
        className={`font-semibold tabular-nums ${
          lote.vencido ? 'text-terracota-700' : 'text-slate-700'
        }`}
      >
        {formatearDias(lote.diasParaVencer)}
      </span>
    ),
  },
  {
    id: 'saldo',
    header: 'Saldo',
    align: 'derecha',
    render: (lote) => (
      <span className="font-semibold tabular-nums text-slate-900">
        {formatearVolumen(lote.cantidadBase, lote.unidadBaseSimbolo)}
      </span>
    ),
  },
  {
    id: 'estado',
    header: 'Estado',
    align: 'centro',
    render: (lote) => {
      const estado = estadoCaducidad(lote, DIAS_UMBRAL);
      return <StatusBadge estado={ESTADO_BADGE[estado]}>{estado}</StatusBadge>;
    },
  },
];

export function LotesFefo() {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const { esSupervision } = useAuth();
  const [pestana, setPestana] = useState<Pestana>('todos');
  const [soloConSaldo, setSoloConSaldo] = useState(false);
  const [abierto, setAbierto] = useState(false);

  const {
    datos: lotes,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(
    () => obtenerLotes({ sucursalId: sedeActiva, soloConSaldo }),
    [sedeActiva, soloConSaldo],
    SIN_DATOS,
  );

  const conteos = useMemo(
    () => ({
      todos: lotes.length,
      porvencer: lotes.filter((l) => estadoCaducidad(l, DIAS_UMBRAL) === 'Por vencer').length,
      vencidos: lotes.filter((l) => l.vencido).length,
      sinfecha: lotes.filter((l) => l.fechaVencimiento === null).length,
    }),
    [lotes],
  );

  const filtrados = useMemo(() => {
    if (pestana === 'porvencer') {
      return lotes.filter((l) => estadoCaducidad(l, DIAS_UMBRAL) === 'Por vencer');
    }
    if (pestana === 'vencidos') {
      return lotes.filter((l) => l.vencido);
    }
    if (pestana === 'sinfecha') {
      return lotes.filter((l) => l.fechaVencimiento === null);
    }
    return lotes;
  }, [lotes, pestana]);

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

      <div className="flex flex-wrap items-center gap-4">
        <p className="min-w-0 flex-1 text-sm text-slate-500">
          {nombreSedeActiva} · orden FEFO: primero el que vence antes
        </p>

        {/* El filtro viaja al servidor, no se aplica aquí: un lote recién creado
            está en cero y el endpoint lo devuelve salvo que se pida lo contrario. */}
        <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-600">
          <input
            type="checkbox"
            checked={soloConSaldo}
            onChange={(evento) => setSoloConSaldo(evento.target.checked)}
            className="h-4 w-4 cursor-pointer rounded border-slate-300 text-terracota-600 focus:ring-terracota-500"
          />
          Solo con existencias
        </label>

        {/* Abrir un lote cambia la ficha con la que se rastrea la mercancía y su
            caducidad: solo supervisión, igual que en la API. */}
        {esSupervision ? (
          <Boton onClick={() => setAbierto(true)}>
            <Plus size={17} aria-hidden="true" />
            Nuevo lote
          </Boton>
        ) : null}
      </div>

      {abierto ? (
        <FormularioLote onCerrar={() => setAbierto(false)} onCreado={recargar} />
      ) : null}

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <DataTable
          columnas={COLUMNAS}
          data={filtrados}
          claveFila={(lote) => lote.id}
          cargando={cargando}
          estadoVacio={VACIO_POR_PESTANA[pestana]}
        />
      )}
    </div>
  );
}
