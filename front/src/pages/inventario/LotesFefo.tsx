import { useMemo, useState } from 'react';
import { Search } from 'lucide-react';
import { useSede } from '../../hooks/useSede';
import { useCatalogos } from '../../hooks/useCatalogos';
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
  const { opcionesProducto } = useCatalogos();
  const [pestana, setPestana] = useState<Pestana>('todos');
  const [soloConSaldo, setSoloConSaldo] = useState(false);
  const [numeroBuscado, setNumeroBuscado] = useState('');
  const [productoFiltro, setProductoFiltro] = useState('');

  const {
    datos: lotes,
    cargando,
    error,
    esPermisos,
  } = useConsulta(
    () => obtenerLotes({ sucursalId: sedeActiva, soloConSaldo }),
    [sedeActiva, soloConSaldo],
    SIN_DATOS,
  );

  /**
   * Los dos buscadores, aplicados ANTES de repartir por pestañas.
   *
   * Ese orden es el que importa: así los contadores de las pestañas cuentan lo
   * que queda tras buscar, y no el total. Buscar un lote y que «Vencidos» siga
   * diciendo 4 cuando la tabla muestra uno haría dudar de cuál de los dos
   * números es el bueno.
   *
   * VAN EN EL CLIENTE Y NO EN EL SERVIDOR porque la lista ya está acotada por
   * sede: la API devuelve lo que el rol puede ver —un operario, solo su sede— y
   * esto filtra dentro de eso. Ningún filtro de aquí amplía lo que se ve.
   */
  const visibles = useMemo(() => {
    const texto = numeroBuscado.trim().toLowerCase();
    const producto = Number(productoFiltro);

    return lotes.filter((lote) => {
      if (texto !== '' && !lote.numeroLote.toLowerCase().includes(texto)) {
        return false;
      }
      if (productoFiltro !== '' && lote.productoId !== producto) {
        return false;
      }
      return true;
    });
  }, [lotes, numeroBuscado, productoFiltro]);

  const conteos = useMemo(
    () => ({
      todos: visibles.length,
      porvencer: visibles.filter((l) => estadoCaducidad(l, DIAS_UMBRAL) === 'Por vencer').length,
      vencidos: visibles.filter((l) => l.vencido).length,
      sinfecha: visibles.filter((l) => l.fechaVencimiento === null).length,
    }),
    [visibles],
  );

  const filtrados = useMemo(() => {
    if (pestana === 'porvencer') {
      return visibles.filter((l) => estadoCaducidad(l, DIAS_UMBRAL) === 'Por vencer');
    }
    if (pestana === 'vencidos') {
      return visibles.filter((l) => l.vencido);
    }
    if (pestana === 'sinfecha') {
      return visibles.filter((l) => l.fechaVencimiento === null);
    }
    return visibles;
  }, [visibles, pestana]);

  const buscando = numeroBuscado.trim() !== '' || productoFiltro !== '';

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

      <div className="flex flex-wrap items-end gap-3">
        {/*
          Buscar por número de lote: es la pregunta de «qué entró con esta
          referencia», que se hace con el número impreso en el envase en la mano.
          Por eso es texto libre y no un selector: el número se lee de la caja.
        */}
        <div className="flex h-11 w-full min-w-0 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 focus-within:border-petroleo-500 sm:w-64">
          <Search size={17} className="shrink-0 text-slate-400" aria-hidden="true" />
          <label htmlFor="buscar-lote" className="sr-only">
            Buscar por número de lote
          </label>
          <input
            id="buscar-lote"
            type="search"
            value={numeroBuscado}
            onChange={(evento) => setNumeroBuscado(evento.target.value)}
            placeholder="Número de lote"
            className="min-w-0 flex-1 border-none bg-transparent text-sm text-slate-900 outline-none placeholder:text-slate-400"
          />
        </div>

        {/*
          El producto SÍ es un listado: son unos pocos y del catálogo de la red,
          así que escribirlo a mano solo produciría búsquedas sin resultados por
          una tilde o una palabra de más.
        */}
        <div className="w-full min-w-0 sm:w-64">
          <label htmlFor="filtrar-producto" className="sr-only">
            Filtrar por producto
          </label>
          <select
            id="filtrar-producto"
            value={productoFiltro}
            onChange={(evento) => setProductoFiltro(evento.target.value)}
            className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 outline-none transition focus:border-petroleo-500"
          >
            <option value="">Todos los productos</option>
            {opcionesProducto.map((opcion) => (
              <option key={opcion.valor} value={opcion.valor}>
                {opcion.texto}
              </option>
            ))}
          </select>
        </div>

        {/* El filtro viaja al servidor, no se aplica aquí: un lote en cero
            existe y el endpoint lo devuelve salvo que se pida lo contrario. */}
        <label className="flex h-11 cursor-pointer items-center gap-2 text-sm text-slate-600">
          <input
            type="checkbox"
            checked={soloConSaldo}
            onChange={(evento) => setSoloConSaldo(evento.target.checked)}
            className="h-4 w-4 cursor-pointer rounded border-slate-300 text-terracota-600 focus:ring-terracota-500"
          />
          Solo con existencias
        </label>

        {buscando ? (
          <button
            type="button"
            onClick={() => {
              setNumeroBuscado('');
              setProductoFiltro('');
            }}
            className="h-11 rounded-lg px-2 text-sm font-semibold text-petroleo-700 underline-offset-2 transition hover:underline"
          >
            Quitar filtros
          </button>
        ) : null}
      </div>

      <p className="text-sm text-slate-500">
        {nombreSedeActiva} · orden FEFO: primero el que vence antes. Los lotes no se crean a mano:
        nacen al recibir una compra o un traslado, con el número que trae el envase.
      </p>

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <DataTable
          columnas={COLUMNAS}
          data={filtrados}
          claveFila={(lote) => lote.id}
          cargando={cargando}
          estadoVacio={
            buscando
              ? 'Ningún lote coincide con la búsqueda en esta pestaña.'
              : VACIO_POR_PESTANA[pestana]
          }
        />
      )}
    </div>
  );
}
