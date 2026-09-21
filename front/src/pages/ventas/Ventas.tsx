import { useMemo, useState } from 'react';
import { Eye, Plus, Search, Tags } from 'lucide-react';
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
import { DetalleVenta } from '../../components/ventas/DetalleVenta';
import {
  formatearCOP,
  formatearEntero,
  formatearFechaHora,
  formatearFechaSolo,
} from '../../utils/formato';

const SIN_DATOS: VentaDto[] = [];

/**
 * Tope de filas que se le pide a la API.
 *
 * SON DOS PORQUE LA PREGUNTA ES DISTINTA. Sin día elegido, la pantalla enseña
 * «lo último que pasó» y cien filas sobran. Con un día elegido, la pregunta es
 * «qué se vendió ESE día», y ahí una lista recortada mentiría en el conteo y en
 * el total de arriba: hay que traer el día entero.
 *
 * 1000 es el máximo que admite la API (`Math.Clamp(limite, 1, 1000)`); pedir
 * más no traería más.
 */
const LIMITE_RECIENTES = 100;
const LIMITE_DIA = 1000;

/**
 * El día de hoy como `AAAA-MM-DD`, en hora LOCAL.
 *
 * Nada de `toISOString()`: esa cadena va en UTC, y en Colombia (UTC-5) entre
 * las 19:00 y la medianoche devolvería el día siguiente. Quien pulsa «Hoy» a
 * las ocho de la noche vería el día equivocado, que es justo cuando se cierra
 * caja.
 */
function hoyLocal(): string {
  const ahora = new Date();
  const mes = `${ahora.getMonth() + 1}`.padStart(2, '0');
  const dia = `${ahora.getDate()}`.padStart(2, '0');
  return `${ahora.getFullYear()}-${mes}-${dia}`;
}

/**
 * El día elegido, convertido en los dos extremos que espera la API.
 *
 * VAN SIN HUSO Y EN HORA LOCAL a propósito: `ventas.fecha` es un `DATETIME` de
 * MySQL, hora de la tienda, y la API compara con `>=` y `<=` sin convertir
 * nada. Pasar la fecha por UTC correría el día cinco horas.
 *
 * `23:59:59` cierra el día de verdad porque la columna no guarda fracciones de
 * segundo: no existe ninguna venta entre 23:59:59 y la medianoche.
 */
function rangoDelDia(dia: string): { desde: string; hasta: string } {
  return { desde: `${dia}T00:00:00`, hasta: `${dia}T23:59:59` };
}

/**
 * Las columnas, como función: la última necesita el manejador de la pantalla.
 *
 * Se arma una sola vez con `useMemo` para no recrear el arreglo -y con él todas
 * las celdas- en cada tecla del buscador.
 */
function columnasDeVentas(onVer: (id: number) => void): ColumnaTabla<VentaDto>[] {
  return [
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
    // daba cero en todas las filas. Para ver qué se vendió está el botón de la
    // última columna, que pide el detalle SOLO de esa venta.
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
    {
      id: 'detalle',
      header: 'Detalle',
      align: 'centro',
      ancho: 'w-28',
      render: (v) => (
        <button
          type="button"
          onClick={() => onVer(v.id)}
          className="inline-flex h-9 items-center gap-1.5 whitespace-nowrap rounded-lg border border-slate-300 bg-white px-3 text-xs font-semibold text-slate-700 transition hover:bg-slate-50 hover:text-slate-900"
        >
          <Eye size={15} aria-hidden="true" />
          Ver
          <span className="sr-only">el detalle de la venta {v.id}</span>
        </button>
      ),
    },
  ];
}

export function Ventas() {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const { esAdminGeneral } = useAuth();
  const { recargar: recargarCatalogos } = useCatalogos();
  const [busqueda, setBusqueda] = useState('');
  /** Día consultado, `AAAA-MM-DD`. Vacío significa «lo más reciente». */
  const [dia, setDia] = useState('');
  const [abierto, setAbierto] = useState(false);
  const [preciosAbierto, setPreciosAbierto] = useState(false);
  /** Venta cuyo detalle está abierto, o `null`. */
  const [ventaVista, setVentaVista] = useState<number | null>(null);

  const {
    datos: ventas,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(
    () => {
      // EL FILTRO DE DÍA VIAJA AL SERVIDOR, no se aplica aquí. Filtrar en el
      // cliente solo podría mirar dentro de las cien filas que ya llegaron, así
      // que un día de hace un mes saldría vacío aunque tuviera ventas.
      const rango = dia === '' ? null : rangoDelDia(dia);

      return obtenerVentas({
        sucursalId: sedeActiva,
        desde: rango?.desde ?? null,
        hasta: rango?.hasta ?? null,
        limite: rango === null ? LIMITE_RECIENTES : LIMITE_DIA,
      });
    },
    [sedeActiva, dia],
    SIN_DATOS,
  );

  const columnas = useMemo(() => columnasDeVentas(setVentaVista), []);

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

  /**
   * El total se suma de lo que SE ESTÁ VIENDO, no de todo lo que llegó.
   *
   * Si se busca un cliente, el conteo y el importe de arriba tienen que ser los
   * de ese cliente; con la suma del total, la frase diría «3 ventas por
   * $12.000.000» mostrando tres filas que suman mucho menos.
   */
  const total = useMemo(
    () => filtradas.reduce((suma, venta) => suma + (venta.total ?? 0), 0),
    [filtradas],
  );

  /** La consulta llegó al tope: puede haber más ventas que no se están viendo. */
  const recortada = ventas.length >= (dia === '' ? LIMITE_RECIENTES : LIMITE_DIA);

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-3">
        <p className="min-w-0 flex-1 text-sm text-slate-500">
          {nombreSedeActiva} · {formatearEntero(filtradas.length)}{' '}
          {filtradas.length === 1 ? 'venta' : 'ventas'}
          {dia === '' ? '' : ` del ${formatearFechaSolo(dia)}`} por{' '}
          <span className="font-semibold text-slate-700">{formatearCOP(total)}</span>
        </p>

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

      <div className="flex flex-wrap items-end gap-3">
        {/*
          CONSULTAR UN DÍA ES LA PREGUNTA DE CIERRE DE CAJA: «qué se vendió el
          martes». Por eso es un solo día y no un rango: el rango contesta otra
          pregunta -la del informe del mes- y esa se hace con el panel.
        */}
        <div className="flex min-w-0 flex-col gap-1.5">
          <label htmlFor="dia-ventas" className="text-sm font-semibold text-slate-700">
            Día
          </label>
          <input
            id="dia-ventas"
            type="date"
            value={dia}
            onChange={(evento) => setDia(evento.target.value)}
            className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm tabular-nums text-slate-900 outline-none transition focus:border-petroleo-500 sm:w-48"
          />
        </div>

        <Boton variante="secundaria" onClick={() => setDia(hoyLocal())}>
          Hoy
        </Boton>

        {dia === '' ? null : (
          <Boton variante="fantasma" onClick={() => setDia('')}>
            Ver las más recientes
          </Boton>
        )}

        <div className="flex h-11 w-full min-w-0 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 focus-within:border-petroleo-500 sm:w-80">
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

      {ventaVista === null ? null : (
        <DetalleVenta ventaId={ventaVista} onCerrar={() => setVentaVista(null)} />
      )}

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <>
          <DataTable
            columnas={columnas}
            data={filtradas}
            claveFila={(v) => v.id}
            cargando={cargando}
            estadoVacio={
              dia === ''
                ? 'No hay ventas registradas para esta sede.'
                : `Esta sede no registró ventas el ${formatearFechaSolo(dia)}.`
            }
          />

          {/* Decirlo importa: sin este aviso, una lista recortada se lee como
              «esto es todo lo que hay» y el total de arriba, como el del día. */}
          {recortada && !cargando ? (
            <Alerta tipo="info">
              Se están mostrando las {formatearEntero(ventas.length)} más recientes, que es el
              tope de la consulta. Puede haber más ventas sin mostrar: elija un día concreto para
              verlo completo.
            </Alerta>
          ) : null}
        </>
      )}
    </div>
  );
}
