import { useMemo, useState } from 'react';
import { PackageCheck, Plus } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerOrdenesCompra } from '../../services/compras';
import { ETIQUETA_ESTADO_ORDEN } from '../../models/compras';
import type { EstadoOrdenCompra, OrdenCompraDto } from '../../models/compras';
import { DataTable } from '../../components/ui/DataTable';
import type { ColumnaTabla } from '../../components/ui/DataTable';
import { StatusBadge } from '../../components/ui/StatusBadge';
import type { EstadoBadge } from '../../components/ui/StatusBadge';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';
import { FormularioOrdenCompra } from '../../components/compras/FormularioOrdenCompra';
import { FormularioRecepcion } from '../../components/compras/FormularioRecepcion';
import { formatearCOP, formatearEntero, formatearFechaSolo } from '../../utils/formato';

const ESTADO_BADGE: Record<EstadoOrdenCompra, EstadoBadge> = {
  Pendiente: 'neutral',
  ParcialmenteRecibida: 'advertencia',
  Recibida: 'exitoso',
  Cancelada: 'inactivo',
};

type Pestana = 'todas' | 'pendientes' | 'recibidas';

const PESTANAS: { id: Pestana; etiqueta: string }[] = [
  { id: 'todas', etiqueta: 'Todas' },
  { id: 'pendientes', etiqueta: 'Por recibir' },
  { id: 'recibidas', etiqueta: 'Recibidas' },
];

const SIN_DATOS: OrdenCompraDto[] = [];

/**
 * Las columnas dependen del rol: la de acciones solo existe para supervisión.
 * Por eso se construyen en una función y no en una constante de módulo.
 */
function construirColumnas(
  onRecibir: ((orden: OrdenCompraDto) => void) | null,
): ColumnaTabla<OrdenCompraDto>[] {
  const columnas: ColumnaTabla<OrdenCompraDto>[] = [
  {
    id: 'numero',
    header: 'Orden',
    render: (o) => <span className="font-semibold tabular-nums text-slate-900">{o.id}</span>,
  },
  { id: 'proveedor', header: 'Proveedor', accessor: 'proveedorNombre' },
  { id: 'sede', header: 'Sede', accessor: 'sucursalNombre' },
  {
    id: 'fecha',
    header: 'Fecha',
    align: 'derecha',
    render: (o) => (
      <span className="tabular-nums text-slate-600">{formatearFechaSolo(o.fecha)}</span>
    ),
  },
  {
    id: 'lineas',
    header: 'Líneas',
    align: 'derecha',
    render: (o) => (
      // Lo que importa de una orden abierta no es cuántas líneas tiene sino
      // cuántas siguen esperando mercancía.
      <span className="tabular-nums text-slate-600">
        {o.lineasPendientes > 0 ? (
          <span className="font-semibold text-amber-700">
            {formatearEntero(o.lineasPendientes)} de {formatearEntero(o.detalles.length)}
          </span>
        ) : (
          formatearEntero(o.detalles.length)
        )}
      </span>
    ),
  },
  {
    id: 'total',
    header: 'Total',
    align: 'derecha',
    render: (o) => (
      <span className="font-semibold tabular-nums text-slate-900">{formatearCOP(o.total)}</span>
    ),
  },
  { id: 'usuario', header: 'Registró', accessor: 'usuarioNombre' },
  {
    id: 'estado',
    header: 'Estado',
    align: 'centro',
    render: (o) =>
      o.estado === null ? (
        <StatusBadge estado="neutral" />
      ) : (
        <StatusBadge estado={ESTADO_BADGE[o.estado]}>
          {ETIQUETA_ESTADO_ORDEN[o.estado]}
        </StatusBadge>
      ),
  },
  ];

  if (onRecibir !== null) {
    columnas.push({
      id: 'acciones',
      header: '',
      align: 'derecha',
      render: (orden) =>
        // Solo tiene sentido en una orden con líneas pendientes: una ya recibida
        // no admite otra recepción y el botón solo llevaría a un 409.
        orden.lineasPendientes > 0 ? (
          <button
            type="button"
            onClick={() => onRecibir(orden)}
            title="Registrar recepción"
            className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-xs font-semibold text-slate-700 transition hover:border-terracota-400 hover:text-terracota-700"
          >
            <PackageCheck size={15} aria-hidden="true" />
            Recibir
          </button>
        ) : null,
    });
  }

  return columnas;
}

export function Compras() {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const { esSupervision } = useAuth();
  const [pestana, setPestana] = useState<Pestana>('todas');
  const [creando, setCreando] = useState(false);
  const [recibiendo, setRecibiendo] = useState<OrdenCompraDto | null>(null);

  const {
    datos: ordenes,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(() => obtenerOrdenesCompra(sedeActiva), [sedeActiva], SIN_DATOS);

  const columnas = useMemo(
    () => construirColumnas(esSupervision ? setRecibiendo : null),
    [esSupervision],
  );

  const conteos = useMemo(
    () => ({
      todas: ordenes.length,
      pendientes: ordenes.filter((o) => o.lineasPendientes > 0).length,
      recibidas: ordenes.filter((o) => o.estado === 'Recibida').length,
    }),
    [ordenes],
  );

  const filtradas = useMemo(() => {
    if (pestana === 'pendientes') {
      return ordenes.filter((o) => o.lineasPendientes > 0);
    }
    if (pestana === 'recibidas') {
      return ordenes.filter((o) => o.estado === 'Recibida');
    }
    return ordenes;
  }, [ordenes, pestana]);

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
          {nombreSedeActiva} · una orden no mueve stock: eso lo hace su recepción
        </p>

        {/* Comprometer dinero con un proveedor es decisión de quien responde por
            el resultado de la sede: solo supervisión, igual que en la API. */}
        {esSupervision ? (
          <Boton onClick={() => setCreando(true)}>
            <Plus size={17} aria-hidden="true" />
            Nueva orden
          </Boton>
        ) : null}
      </div>

      {creando ? (
        <FormularioOrdenCompra onCerrar={() => setCreando(false)} onCreada={recargar} />
      ) : null}

      {recibiendo ? (
        <FormularioRecepcion
          orden={recibiendo}
          onCerrar={() => setRecibiendo(null)}
          onRecibida={recargar}
        />
      ) : null}

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <DataTable
          columnas={columnas}
          data={filtradas}
          claveFila={(o) => o.id}
          cargando={cargando}
          estadoVacio="No hay órdenes de compra registradas para esta sede."
        />
      )}
    </div>
  );
}
