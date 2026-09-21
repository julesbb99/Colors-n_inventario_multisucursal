import { useMemo } from 'react';
import { TrendingDown, TrendingUp } from 'lucide-react';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerRotacionProductos } from '../../services/dashboard';
import { ETIQUETA_ROTACION } from '../../models/dashboard';
import type { RotacionProductoDto, RotacionProductosDto } from '../../models/dashboard';
import { Alerta } from '../ui/Alerta';
import { ChipColor } from '../ui/ChipColor';
import { CargandoPanel } from '../ui/Spinner';
import { StatusBadge } from '../ui/StatusBadge';
import type { EstadoBadge } from '../ui/StatusBadge';
import { formatearCOP, formatearEntero, formatearVolumen } from '../../utils/formato';

const SIN_DATOS: RotacionProductosDto | null = null;

/** Cuántas filas se muestran de cada lado. El resto vive en el conteo de arriba. */
const FILAS_POR_LADO = 5;

/**
 * El tono de cada clase.
 *
 * ALTA VA EN VERDE Y BAJA EN TERRACOTA, que es al revés de lo que sugiere la
 * palabra «alerta»: una rotación alta es buena —el producto se vende— y una
 * baja es plata quieta en el estante que además puede caducar. El color dice
 * si hay que actuar, no si el número es grande.
 */
const TONO: Record<string, EstadoBadge> = {
  Alta: 'activo',
  Media: 'neutral',
  Baja: 'advertencia',
  SinMovimiento: 'critico',
};

function Dias({ valor }: { valor: number | null }) {
  if (valor === null) {
    return <span className="text-slate-400">—</span>;
  }
  return (
    <span className="tabular-nums text-slate-700">
      {formatearEntero(Math.round(valor))} d
    </span>
  );
}

function Tabla({
  titulo,
  ayuda,
  Icono,
  filas,
  vacio,
}: {
  titulo: string;
  ayuda: string;
  Icono: typeof TrendingUp;
  filas: RotacionProductoDto[];
  vacio: string;
}) {
  return (
    <div className="flex min-w-0 flex-1 flex-col gap-2">
      <div>
        <h3 className="flex items-center gap-2 text-sm font-bold text-slate-900">
          <Icono size={16} aria-hidden="true" />
          {titulo}
        </h3>
        <p className="text-xs text-slate-500">{ayuda}</p>
      </div>

      {filas.length === 0 ? (
        <p className="rounded-xl bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">
          {vacio}
        </p>
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200">
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-sm">
              <thead>
                <tr className="border-b border-petroleo-100 bg-petroleo-50 text-[11px] font-semibold uppercase tracking-wider text-petroleo-800">
                  <th scope="col" className="px-3 py-2.5 text-left">Producto</th>
                  <th scope="col" className="px-3 py-2.5 text-right">Vendido</th>
                  <th scope="col" className="px-3 py-2.5 text-right">En bodega</th>
                  <th scope="col" className="px-3 py-2.5 text-right">Cobertura</th>
                  <th scope="col" className="px-3 py-2.5 text-center">Clase</th>
                </tr>
              </thead>
              <tbody>
                {filas.map((p) => (
                  <tr
                    key={p.productoId}
                    className="border-b border-slate-100 last:border-0 even:bg-slate-50/70"
                    title={
                      p.vecesQueRoto === null
                        ? 'Sin saldo en bodega: no se puede medir cuántas veces rotó.'
                        : `Rotó ${p.vecesQueRoto.toLocaleString('es-CO', { maximumFractionDigits: 2 })} veces · ${formatearCOP(p.totalFacturado)} facturados`
                    }
                  >
                    <td className="px-3 py-2.5">
                      <span className="flex items-center gap-2">
                        <ChipColor nombre={p.productoNombre} alto={22} />
                        <span className="text-slate-800">{p.productoNombre}</span>
                      </span>
                    </td>
                    <td className="px-3 py-2.5 text-right whitespace-nowrap tabular-nums text-slate-700">
                      {formatearVolumen(p.cantidadVendidaBase, p.unidadBaseSimbolo)}
                    </td>
                    <td className="px-3 py-2.5 text-right whitespace-nowrap tabular-nums text-slate-600">
                      {formatearVolumen(p.saldoActualBase, p.unidadBaseSimbolo)}
                    </td>
                    <td className="px-3 py-2.5 text-right whitespace-nowrap">
                      <Dias valor={p.diasCobertura} />
                    </td>
                    <td className="px-3 py-2.5 text-center">
                      <StatusBadge estado={TONO[p.clase] ?? 'neutral'}>
                        {ETIQUETA_ROTACION[p.clase]}
                      </StatusBadge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

/**
 * Rotación del catálogo: qué se mueve y qué lleva meses quieto.
 *
 * SE CLASIFICA POR DÍAS DE COBERTURA —cuánto aguanta el stock al ritmo del
 * periodo— y no por cantidad vendida. «Se vendieron 400 litros» no dice si eso
 * es mucho sin saber cuánto hay en bodega; los días sí, y además son
 * comparables entre productos de unidades distintas: un galón y un litro no se
 * comparan, treinta días y noventa sí.
 */
export function RotacionProductos() {
  const { sedeActiva, nombreSedeActiva } = useSede();

  const { datos, cargando, error, esPermisos } = useConsulta(
    () => obtenerRotacionProductos(sedeActiva),
    [sedeActiva],
    SIN_DATOS,
  );

  // La lista llega ordenada de más a menos demanda, con los sin movimiento al
  // final. Alta demanda es el principio; baja demanda, el final invertido.
  const alta = useMemo(
    () => (datos?.productos ?? []).filter((p) => p.clase === 'Alta').slice(0, FILAS_POR_LADO),
    [datos],
  );

  const baja = useMemo(
    () =>
      (datos?.productos ?? [])
        // Los sin movimiento entran aquí -son el caso extremo de baja demanda-
        // pero de PRIMEROS: un producto que no se vendió nada es más urgente
        // que uno que se vende despacio.
        .filter((p) => p.clase === 'Baja' || p.clase === 'SinMovimiento')
        .sort((a, b) => {
          const pesoA = a.clase === 'SinMovimiento' ? 0 : 1;
          const pesoB = b.clase === 'SinMovimiento' ? 0 : 1;
          if (pesoA !== pesoB) {
            return pesoA - pesoB;
          }
          return (b.diasCobertura ?? 0) - (a.diasCobertura ?? 0);
        })
        .slice(0, FILAS_POR_LADO),
    [datos],
  );

  return (
    <section className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5">
      <div>
        <h2 className="text-sm font-bold text-slate-900">Rotación de productos</h2>
        <p className="text-xs text-slate-500">
          {nombreSedeActiva}
          {datos === null
            ? ''
            : ` · últimos ${datos.dias} días · alta hasta ${datos.umbralDiasAlta} días de cobertura, baja desde ${datos.umbralDiasBaja}`}
        </p>
      </div>

      {cargando ? <CargandoPanel texto="Calculando la rotación…" /> : null}

      {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}

      {datos !== null && !cargando ? (
        <>
          <div className="flex flex-wrap gap-2 text-xs">
            <span className="rounded-full bg-emerald-50 px-2.5 py-1 font-semibold text-emerald-800 ring-1 ring-inset ring-emerald-200">
              {datos.conAlta} de alta demanda
            </span>
            <span className="rounded-full bg-slate-100 px-2.5 py-1 font-semibold text-slate-700 ring-1 ring-inset ring-slate-300">
              {datos.conMedia} media
            </span>
            <span className="rounded-full bg-amber-50 px-2.5 py-1 font-semibold text-amber-800 ring-1 ring-inset ring-amber-200">
              {datos.conBaja} de baja demanda
            </span>
            <span className="rounded-full bg-terracota-50 px-2.5 py-1 font-semibold text-terracota-700 ring-1 ring-inset ring-terracota-200">
              {datos.sinMovimiento} sin movimiento
            </span>
          </div>

          <div className="flex flex-col gap-5 lg:flex-row">
            <Tabla
              titulo="Alta demanda"
              ayuda="Se venden más rápido de lo que dura el stock. Hay que reponer pronto."
              Icono={TrendingUp}
              filas={alta}
              vacio="Ningún producto rota tan rápido en este periodo."
            />

            <Tabla
              titulo="Baja demanda"
              ayuda="Plata quieta en el estante, y producto que puede caducar antes de venderse."
              Icono={TrendingDown}
              filas={baja}
              vacio="Todo el catálogo se está moviendo."
            />
          </div>

          <p className="text-xs text-slate-500">
            La <strong>cobertura</strong> es cuántos días aguanta el stock actual al ritmo de este
            periodo. Se clasifica por ella y no por cantidad vendida: «400 litros» no dice si es
            mucho sin saber cuánto hay en bodega, y los días sí se comparan entre productos de
            unidades distintas. «Sin movimiento» no es rotación baja: la baja se calcula, esa no
            se puede calcular porque no hay ritmo.
          </p>
        </>
      ) : null}
    </section>
  );
}
