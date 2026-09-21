import { useState } from 'react';
import { useAuth } from '../../hooks/useAuth';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerComparativaSucursales } from '../../services/dashboard';
import type { ComparativaSucursalesDto } from '../../models/dashboard';
import { Alerta } from '../ui/Alerta';
import { CargandoPanel } from '../ui/Spinner';
import { formatearCOP, formatearEntero, formatearLitros } from '../../utils/formato';

const SIN_DATOS: ComparativaSucursalesDto | null = null;

/** `AAAA-MM-DD` en hora LOCAL. Nada de `toISOString()`: en UTC−5 corre el día. */
function comoFecha(fecha: Date): string {
  const mes = String(fecha.getMonth() + 1).padStart(2, '0');
  const dia = String(fecha.getDate()).padStart(2, '0');
  return `${fecha.getFullYear()}-${mes}-${dia}`;
}

const PERIODOS = [
  { id: 'mes', etiqueta: 'Este mes' },
  { id: 'trimestre', etiqueta: '90 días' },
  { id: 'anio', etiqueta: 'Este año' },
] as const;

type Periodo = (typeof PERIODOS)[number]['id'];

function rangoDe(periodo: Periodo): { desde: string; hasta: string } {
  const hoy = new Date();
  const hasta = comoFecha(hoy);

  if (periodo === 'trimestre') {
    const inicio = new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate() - 89);
    return { desde: comoFecha(inicio), hasta };
  }

  if (periodo === 'anio') {
    return { desde: comoFecha(new Date(hoy.getFullYear(), 0, 1)), hasta };
  }

  return { desde: comoFecha(new Date(hoy.getFullYear(), hoy.getMonth(), 1)), hasta };
}

/**
 * Comparativa de rendimiento entre las sedes de la red.
 *
 * SOLO ADMINISTRACIÓN Y GERENCIA. Quien la monta comprueba el rol antes de
 * pintarla, pero eso no es el control: la API responde 403 al operario, y
 * además lo vuelve a comprobar el servicio. Esto solo evita pedir algo que va a
 * fallar.
 *
 * NO APLICA EL AISLAMIENTO POR SEDE, a diferencia del resto del panel, y es
 * deliberado: una comparativa en la que cada gerente solo ve su propia fila no
 * es una comparativa, no hay contra qué comparar. Lo que se expone son
 * AGREGADOS —ninguna venta concreta, ningún cliente, ningún precio—, así que
 * quien la lea sabe que una sede vendió más, no a quién ni a cómo.
 */
export function ComparativaSucursales() {
  const { sesion } = useAuth();
  const [periodo, setPeriodo] = useState<Periodo>('mes');

  const { desde, hasta } = rangoDe(periodo);

  const { datos, cargando, error, esPermisos } = useConsulta(
    () => obtenerComparativaSucursales(desde, hasta),
    [desde, hasta],
    SIN_DATOS,
  );

  /** La sede de quien mira. Nula en el administrador, que no tiene una propia. */
  const sedePropia = sesion?.sucursalId ?? null;

  return (
    <section className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5">
      <div className="flex flex-wrap items-center gap-3">
        <div className="min-w-0 flex-1">
          <h2 className="text-sm font-bold text-slate-900">Rendimiento entre sucursales</h2>
          <p className="text-xs text-slate-500">
            Todas las sedes de la red · solo administración y gerencia
          </p>
        </div>

        <div className="flex items-center gap-1 rounded-lg bg-slate-100 p-1">
          {PERIODOS.map(({ id, etiqueta }) => (
            <button
              key={id}
              type="button"
              onClick={() => setPeriodo(id)}
              aria-pressed={id === periodo}
              className={`h-8 rounded-md px-3 text-xs font-semibold transition ${
                id === periodo
                  ? 'bg-white text-slate-900 shadow-sm'
                  : 'text-slate-500 hover:text-slate-800'
              }`}
            >
              {etiqueta}
            </button>
          ))}
        </div>
      </div>

      {cargando ? <CargandoPanel texto="Comparando sedes…" /> : null}

      {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}

      {datos !== null && !cargando ? (
        <>
          <div className="overflow-hidden rounded-xl border border-slate-200">
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-sm">
                <thead>
                  <tr className="border-b border-petroleo-100 bg-petroleo-50 text-[11px] font-semibold uppercase tracking-wider text-petroleo-800">
                    <th scope="col" className="px-3 py-2.5 text-left">Sede</th>
                    <th scope="col" className="px-3 py-2.5 text-right">Vendido</th>
                    <th scope="col" className="px-3 py-2.5 text-right">Part.</th>
                    <th scope="col" className="px-3 py-2.5 text-right">Ventas</th>
                    <th scope="col" className="px-3 py-2.5 text-right">Ticket</th>
                    <th scope="col" className="px-3 py-2.5 text-right">En bodega</th>
                    <th scope="col" className="px-3 py-2.5 text-right">$/L</th>
                    <th scope="col" className="px-3 py-2.5 text-right">Alertas</th>
                    <th scope="col" className="px-3 py-2.5 text-right">Traslados</th>
                  </tr>
                </thead>
                <tbody>
                  {datos.sucursales.map((s) => {
                    const esPropia = s.sucursalId === sedePropia;

                    return (
                      <tr
                        key={s.sucursalId}
                        className={`border-b border-slate-100 last:border-0 ${
                          // La sede propia se marca para que el gerente se
                          // encuentre a sí mismo sin leer los tres nombres.
                          esPropia ? 'bg-petroleo-50/70' : 'even:bg-slate-50/70'
                        }`}
                      >
                        <td className="px-3 py-2.5">
                          <span className="font-medium text-slate-900">{s.sucursalNombre}</span>
                          {esPropia ? (
                            <span className="ml-2 rounded-full bg-petroleo-800 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide text-white">
                              tuya
                            </span>
                          ) : null}
                          {s.ciudad === null ? null : (
                            <span className="block text-xs text-slate-500">{s.ciudad}</span>
                          )}
                        </td>
                        <td className="whitespace-nowrap px-3 py-2.5 text-right font-semibold tabular-nums text-slate-900">
                          {formatearCOP(s.totalVendido)}
                        </td>
                        <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums">
                          {/* Nula cuando la red no vendió nada: sin tarta que
                              repartir, un 0 % sugeriría que esta sede se quedó
                              fuera de algo. */}
                          {s.participacionPorcentaje === null ? (
                            <span className="text-slate-400">—</span>
                          ) : (
                            <span className="font-semibold text-petroleo-800">
                              {s.participacionPorcentaje.toLocaleString('es-CO', {
                                maximumFractionDigits: 1,
                              })}{' '}
                              %
                            </span>
                          )}
                        </td>
                        <td className="px-3 py-2.5 text-right tabular-nums text-slate-600">
                          {formatearEntero(s.cantidadVentas)}
                        </td>
                        <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums text-slate-600">
                          {s.cantidadVentas === 0 ? '—' : formatearCOP(s.ticketPromedio)}
                        </td>
                        <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums text-slate-600">
                          {formatearLitros(s.saldoLitros)}
                        </td>
                        <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums text-slate-600">
                          {s.ventaPorLitroEnBodega === null
                            ? '—'
                            : formatearCOP(s.ventaPorLitroEnBodega)}
                        </td>
                        <td className="px-3 py-2.5 text-right tabular-nums">
                          <span
                            className={
                              s.productosEnAlerta > 0
                                ? 'font-semibold text-terracota-700'
                                : 'text-slate-300'
                            }
                          >
                            {formatearEntero(s.productosEnAlerta)}
                          </span>
                        </td>
                        <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums text-slate-600">
                          {formatearEntero(s.trasladosDespachados)} ↑{' '}
                          {formatearEntero(s.trasladosRecibidos)} ↓
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
                <tfoot>
                  <tr className="border-t-2 border-petroleo-200 bg-petroleo-50/60 font-semibold">
                    <td className="px-3 py-2.5 text-slate-800">Toda la red</td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums text-slate-900">
                      {formatearCOP(datos.totalRedVendido)}
                    </td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-slate-500">100 %</td>
                    <td className="px-3 py-2.5 text-right tabular-nums text-slate-700">
                      {formatearEntero(datos.totalRedVentas)}
                    </td>
                    <td colSpan={5} />
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>

          <p className="text-xs text-slate-500">
            <strong>$/L</strong> son los pesos vendidos por cada litro almacenado: mide la
            productividad del inventario, que es lo que permite comparar una sede grande con una
            pequeña —el total en pesos siempre favorece a la grande—. En traslados, ↑ es lo que
            la sede despachó y ↓ lo que recibió.
          </p>
        </>
      ) : null}
    </section>
  );
}
