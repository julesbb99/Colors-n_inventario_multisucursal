import { useMemo, useState } from 'react';
import { Plus, Search, Tags } from 'lucide-react';
import { useSede } from '../../hooks/useSede';
import { useAuth } from '../../hooks/useAuth';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerVentas } from '../../services/ventas';
import type { VentaDto } from '../../models/ventas';
import { DataTable } from '../../components/ui/DataTable';
import type { ColumnaTabla } from '../../components/ui/DataTable';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';
import { FormularioVenta } from '../../components/ventas/FormularioVenta';
import { ListaPreciosVenta } from '../../components/ventas/ListaPreciosVenta';
import { formatearCOP, formatearEntero, formatearFechaHora } from '../../utils/formato';

const SIN_DATOS: VentaDto[] = [];

const COLUMNAS: ColumnaTabla<VentaDto>[] = [
  {
    id: 'numero',
    header: 'Venta',
    render: (v) => <span className="font-semibold tabular-nums text-slate-900">{v.id}</span>,
  },
  {
    id: 'cliente',
    header: 'Cliente',
    render: (v) => (
      <div className="min-w-0">
        <div className="truncate font-medium text-slate-900">{v.clienteRazonSocial}</div>
        <div className="truncate text-xs tabular-nums text-slate-500">{v.clienteDocumento}</div>
      </div>
    ),
  },
  { id: 'sede', header: 'Sede', accessor: 'sucursalNombre' },
  {
    id: 'fecha',
    header: 'Fecha',
    align: 'derecha',
    render: (v) => (
      <span className="tabular-nums text-slate-600">{formatearFechaHora(v.fecha)}</span>
    ),
  },
  // SIN COLUMNA DE LÍNEAS. `GET /api/ventas` no trae el detalle -cargarlo para
  // cien ventas convertiría una consulta en ciento una-, así que contar aquí
  // daba cero en todas las filas. Se quitó en vez de arreglarse porque el dato
  // no se estaba usando para nada: para ver las líneas se abre la venta.
  {
    id: 'total',
    header: 'Total',
    align: 'derecha',
    render: (v) => (
      // `total` es nulo si la venta quedó sin encabezado calculado. Se muestra el
      // hueco en vez de un 0, que se leería como una venta de cero pesos.
      <span className="font-semibold tabular-nums text-slate-900">
        {v.total === null ? '—' : formatearCOP(v.total)}
      </span>
    ),
  },
  { id: 'usuario', header: 'Registró', accessor: 'usuarioNombre' },
];

export function Ventas() {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const { esAdminGeneral } = useAuth();
  const { recargar: recargarCatalogos } = useCatalogos();
  const [busqueda, setBusqueda] = useState('');
  const [abierto, setAbierto] = useState(false);
  const [preciosAbierto, setPreciosAbierto] = useState(false);

  const {
    datos: ventas,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(() => obtenerVentas({ sucursalId: sedeActiva }), [sedeActiva], SIN_DATOS);

  const total = useMemo(
    () => ventas.reduce((suma, venta) => suma + (venta.total ?? 0), 0),
    [ventas],
  );

  const filtradas = useMemo(() => {
    const texto = busqueda.trim().toLowerCase();
    if (texto === '') {
      return ventas;
    }
    return ventas.filter(
      (venta) =>
        venta.clienteRazonSocial.toLowerCase().includes(texto) ||
        venta.clienteDocumento.toLowerCase().includes(texto),
    );
  }, [ventas, busqueda]);

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-3">
        <p className="min-w-0 flex-1 text-sm text-slate-500">
          {nombreSedeActiva} · {formatearEntero(ventas.length)} ventas por{' '}
          <span className="font-semibold text-slate-700">{formatearCOP(total)}</span>
        </p>

        <div className="flex h-11 w-full items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 focus-within:border-petroleo-500 sm:w-80">
          <Search size={17} className="shrink-0 text-slate-400" aria-hidden="true" />
          <label htmlFor="buscar-ventas" className="sr-only">
            Buscar por cliente o documento
          </label>
          <input
            id="buscar-ventas"
            type="search"
            value={busqueda}
            onChange={(evento) => setBusqueda(evento.target.value)}
            placeholder="Buscar por cliente o documento"
            className="min-w-0 flex-1 border-none bg-transparent text-sm text-slate-900 outline-none placeholder:text-slate-400"
          />
        </div>

        {/* Los precios de venta son de toda la red y las tres sedes los heredan:
            solo el Administrador General los toca, igual que en la API. */}
        {esAdminGeneral ? (
          <Boton variante="secundaria" onClick={() => setPreciosAbierto(true)}>
            <Tags size={17} aria-hidden="true" />
            Lista de precios
          </Boton>
        ) : null}

        {/* Registrar una venta la puede hacer cualquier rol: es el día a día del
            mostrador, no una decisión de supervisión. */}
        <Boton onClick={() => setAbierto(true)}>
          <Plus size={17} aria-hidden="true" />
          Registrar venta
        </Boton>
      </div>

      {abierto ? (
        <FormularioVenta onCerrar={() => setAbierto(false)} onRegistrada={recargar} />
      ) : null}

      {preciosAbierto ? (
        <ListaPreciosVenta
          onCerrar={() => setPreciosAbierto(false)}
          // El catálogo lleva el precio: sin recargarlo, el formulario de venta
          // seguiría rellenando con el viejo hasta recargar la página.
          onGuardado={recargarCatalogos}
        />
      ) : null}

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <DataTable
          columnas={COLUMNAS}
          data={filtradas}
          claveFila={(v) => v.id}
          cargando={cargando}
          estadoVacio="No hay ventas registradas para esta sede."
        />
      )}
    </div>
  );
}
