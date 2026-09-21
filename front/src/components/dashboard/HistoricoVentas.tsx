import { useMemo, useState } from 'react';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerHistoricoVentas } from '../../services/dashboard';
import type { HistoricoVentasDto } from '../../models/dashboard';
import { Alerta } from '../ui/Alerta';
import { CargandoPanel } from '../ui/Spinner';
import { formatearCOP, formatearEntero, formatearFechaSolo } from '../../utils/formato';

const SIN_DATOS: HistoricoVentasDto | null = null;

const NOMBRE_MES = [
  'ene', 'feb', 'mar', 'abr', 'may', 'jun',
  'jul', 'ago', 'sep', 'oct', 'nov', 'dic',
] as const;

const VENTANAS = [
  { meses: 6, etiqueta: '6 meses' },
  { meses: 12, etiqueta: '12 meses' },
  { meses: 24, etiqueta: '24 meses' },
] as const;

/**
 * Las ventas mes a mes, con el acumulado de toda la historia.
 *
 * POR QUE EXISTE. El resumen de arriba solo decía «del día» y «del mes», y el
 * mes en curso siempre arranca en cero: el día 1 el panel parecía el de una
 * empresa que nunca ha vendido nada. Aquí se ve de dónde viene ese mes.
 *
 * LA GRÁFICA ES CSS, sin librería. Son doce barras y una escala: meter una
 * dependencia de gráficas por esto añadiría 200 kB al paquete para dibujar
 * rectángulos.
 */
export function HistoricoVentas() {
  const { sedeActiva } = useSede();
  const [meses, setMeses] = useState(12);

  const { datos, cargando, error, esPermisos } = useConsulta(
    () => obtenerHistoricoVentas(sedeActiva, meses),
    [sedeActiva, meses],
    SIN_DATOS,
  );

  /**
   * Los meses SIN VENTAS se rellenan aquí, no en el servidor.
   *
   * La API devuelve solo los meses que tuvieron ventas, y con razón: inventar
   * filas vacías en una consulta agregada es trabajo que no le toca. Pero una
   * gráfica a la que le faltan los meses flojos miente sobre la tendencia —dos
   * barras juntas parecen consecutivas cuando entre ellas hubo un mes en cero—,
   * así que el hueco se dibuja.
   */
  const barras = useMemo(() => {
    if (datos === null) {
      return [];
    }

    const porClave = new Map(datos.meses.map((m) => [`${m.anio}-${m.mes}`, m]));
    const hoy = new Date();
    const serie: { clave: string; anio: number; mes: number; total: number; ventas: number }[] = [];

    for (let i = datos.mesesSolicitados - 1; i >= 0; i -= 1) {
      // Construido con componentes locales: `new Date(año, mes - 1 - i)`
      // normaliza solo el desbordamiento de mes, sin pasar por UTC.
      const fecha = new Date(hoy.getFullYear(), hoy.getMonth() - i, 1);
      const anio = fecha.getFullYear();
      const mes = fecha.getMonth() + 1;
      const clave = `${anio}-${mes}`;
      const encontrado = porClave.get(clave);

      serie.push({
        clave,
        anio,
        mes,
        total: encontrado?.total ?? 0,
        ventas: encontrado?.cantidadVentas ?? 0,
      });
    }

    return serie;
  }, [datos]);

  const maximo = useMemo(
    () => barras.reduce((mayor, b) => Math.max(mayor, b.total), 0),
    [barras],
  );

  return (
    <section className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="min-w-0 flex-1 text-sm font-bold text-slate-900">
          Ventas por mes e histórico
        </h2>

        <div className="flex items-center gap-1 rounded-lg bg-slate-100 p-1">
          {VENTANAS.map(({ meses: n, etiqueta }) => (
            <button
              key={n}
              type="button"
              onClick={() => setMeses(n)}
              aria-pressed={n === meses}
              className={`h-8 rounded-md px-3 text-xs font-semibold transition ${
                n === meses
                  ? 'bg-white text-slate-900 shadow-sm'
                  : 'text-slate-500 hover:text-slate-800'
              }`}
            >
              {etiqueta}
            </button>
          ))}
        </div>
      </div>

      {cargando ? <CargandoPanel texto="Cargando el histórico…" /> : null}

      {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}

      {datos !== null && !cargando ? (
        <>
          <dl className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            {/*
              EL ACUMULADO ES LA CIFRA NUEVA y por eso va primera: es «la suma
              de todas las ventas realizadas», y no sale de sumar la gráfica de
              al lado —esa está recortada a la ventana—.
            */}
            <div>
              <dt className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                Vendido en total
              </dt>
              <dd className="mt-0.5 text-lg font-bold tabular-nums text-slate-900">
                {formatearCOP(datos.totalHistorico)}
              </dd>
              <dd className="text-xs text-slate-500">
                {formatearEntero(datos.cantidadHistorica)}{' '}
                {datos.cantidadHistorica === 1 ? 'venta' : 'ventas'} desde{' '}
                {formatearFechaSolo(datos.primeraVenta)}
              </dd>
            </div>

            <div>
              <dt className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                Promedio mensual
              </dt>
              <dd className="mt-0.5 text-lg font-bold tabular-nums text-slate-900">
                {formatearCOP(datos.promedioMensual)}
              </dd>
              {/* Se dice sobre qué se divide: el número cambia mucho según se
                  cuenten los meses con ventas o los del calendario. */}
              <dd className="text-xs text-slate-500">
                sobre los meses que tuvieron ventas
              </dd>
            </div>

            <div>
              <dt className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                Ticket promedio
              </dt>
              <dd className="mt-0.5 text-lg font-bold tabular-nums text-slate-900">
                {formatearCOP(datos.ticketPromedioHistorico)}
              </dd>
              <dd className="text-xs text-slate-500">de toda la historia</dd>
            </div>

            <div>
              <dt className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                Última venta
              </dt>
              <dd className="mt-0.5 text-lg font-bold tabular-nums text-slate-900">
                {formatearFechaSolo(datos.ultimaVenta)}
              </dd>
              <dd className="text-xs text-slate-500">
                {datos.meses.length}{' '}
                {datos.meses.length === 1 ? 'mes con ventas' : 'meses con ventas'}
              </dd>
            </div>
          </dl>

          {maximo <= 0 ? (
            <p className="rounded-xl bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">
              No hay ventas registradas en los últimos {datos.mesesSolicitados} meses.
            </p>
          ) : (
            <div className="flex items-end gap-1.5 overflow-x-auto pb-1" style={{ height: 160 }}>
              {barras.map((b) => {
                const alto = Math.round((b.total / maximo) * 100);

                return (
                  <div
                    key={b.clave}
                    className="flex min-w-[34px] flex-1 flex-col items-center justify-end gap-1"
                    style={{ height: '100%' }}
                    title={`${NOMBRE_MES[b.mes - 1]} ${b.anio}: ${formatearCOP(b.total)} en ${formatearEntero(b.ventas)} ventas`}
                  >
                    <span className="text-[10px] font-semibold tabular-nums text-slate-500">
                      {b.total > 0 ? formatearEntero(b.ventas) : ''}
                    </span>
                    {/*
                      Altura mínima de 2 px en los meses con cero: una barra de
                      altura cero es indistinguible de una columna que no se
                      dibujó, y el mes en cero es justo lo que hay que ver.
                    */}
                    <div
                      className={`w-full rounded-t ${
                        b.total > 0 ? 'bg-terracota-500' : 'bg-slate-200'
                      }`}
                      style={{ height: `${Math.max(alto, 2)}%` }}
                    />
                    <span className="whitespace-nowrap text-[10px] text-slate-500">
                      {NOMBRE_MES[b.mes - 1]}
                      {/* El año solo en enero y en la primera barra: repetirlo
                          en las doce llena la escala de ruido. */}
                      {b.mes === 1 || b.clave === barras[0].clave ? ` ${String(b.anio).slice(2)}` : ''}
                    </span>
                  </div>
                );
              })}
            </div>
          )}
        </>
      ) : null}
    </section>
  );
}
