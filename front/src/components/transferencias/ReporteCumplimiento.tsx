import { useState } from 'react';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerReporteCumplimiento } from '../../services/transferencias';
import type {
  CumplimientoGrupoDto,
  ReporteCumplimientoDto,
} from '../../models/transferencias';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { CargandoPanel } from '../ui/Spinner';
import { formatearEntero } from '../../utils/formato';

const SIN_REPORTE: ReporteCumplimientoDto | null = null;

/**
 * Hace `meses` meses, como `AAAA-MM-DD` LOCAL.
 *
 * Con componentes locales y no `toISOString()`: esa cadena va en UTC y en
 * Colombia (UTC−5) corre el día durante toda la tarde.
 */
function haceMeses(meses: number): string {
  const fecha = new Date();
  fecha.setMonth(fecha.getMonth() - meses);

  const mes = String(fecha.getMonth() + 1).padStart(2, '0');
  const dia = String(fecha.getDate()).padStart(2, '0');
  return `${fecha.getFullYear()}-${mes}-${dia}`;
}

function hoyLocal(): string {
  const ahora = new Date();
  const mes = String(ahora.getMonth() + 1).padStart(2, '0');
  const dia = String(ahora.getDate()).padStart(2, '0');
  return `${ahora.getFullYear()}-${mes}-${dia}`;
}

/**
 * Un porcentaje con su color.
 *
 * NULO NO ES CERO, y por eso se pinta «—» y no «0 %»: una ruta por la que
 * todavía no ha llegado nada no ha incumplido nada. Un 0 % rojo la acusaría de
 * algo que no hizo.
 *
 * Los cortes son 90 y 70 por ser los que se usan hablando de servicio; no salen
 * de ningún dato del sistema, así que si la empresa fija otros se cambian aquí.
 */
function Porcentaje({ valor }: { valor: number | null }) {
  if (valor === null) {
    return <span className="text-slate-400">—</span>;
  }

  const tono =
    valor >= 90
      ? 'text-emerald-700'
      : valor >= 70
        ? 'text-amber-700'
        : 'text-terracota-700';

  return (
    <span className={`font-semibold tabular-nums ${tono}`}>
      {valor.toLocaleString('es-CO', { maximumFractionDigits: 1 })} %
    </span>
  );
}

/** Una celda de conteo. El cero se apaga: lo que importa es lo que no es cero. */
function Conteo({ valor, alerta = false }: { valor: number; alerta?: boolean }) {
  return (
    <span
      className={`tabular-nums ${
        valor === 0
          ? 'text-slate-300'
          : alerta
            ? 'font-semibold text-terracota-700'
            : 'text-slate-700'
      }`}
    >
      {formatearEntero(valor)}
    </span>
  );
}

const ENCABEZADOS = [
  { id: 'grupo', texto: '', align: 'text-left' },
  { id: 'solicitados', texto: 'Pedidos', align: 'text-right' },
  { id: 'despachados', texto: 'Despach.', align: 'text-right' },
  // RECHAZADOS Y CANCELADOS: los dos desenlaces en que el traslado nunca sale.
  // Se calculaban desde el principio y no se mostraban, y sin ellos la fila no
  // cuadra a ojo: se veian 2 pedidos y 1 despachado sin poder saber qué pasó
  // con el otro. Van JUNTO a «Despach.» por eso, para cerrar la resta.
  { id: 'rechazados', texto: 'Rechaz.', align: 'text-right' },
  { id: 'cancelados', texto: 'Cancel.', align: 'text-right' },
  { id: 'recibidos', texto: 'Recibidos', align: 'text-right' },
  { id: 'parciales', texto: 'Cortos', align: 'text-right' },
  { id: 'tarde', texto: 'Tarde', align: 'text-right' },
  { id: 'abiertas', texto: 'Pend.', align: 'text-right' },
  { id: 'dias', texto: 'Días', align: 'text-right' },
  { id: 'atencion', texto: 'Atención', align: 'text-right' },
  { id: 'cantidad', texto: 'Cantidad', align: 'text-right' },
  { id: 'plazo', texto: 'Plazo', align: 'text-right' },
] as const;

function Fila({ grupo, destacada = false }: { grupo: CumplimientoGrupoDto; destacada?: boolean }) {
  return (
    <tr
      className={
        destacada
          ? 'border-t-2 border-petroleo-200 bg-petroleo-50/60 font-semibold'
          : 'border-b border-slate-100 last:border-0 even:bg-slate-50/70'
      }
    >
      <td className="px-3 py-2.5 text-slate-800">{grupo.etiqueta}</td>
      <td className="px-3 py-2.5 text-right">
        <Conteo valor={grupo.solicitados} />
      </td>
      <td className="px-3 py-2.5 text-right">
        <Conteo valor={grupo.despachados} />
      </td>
      {/*
        RECHAZADO ES DEL ORIGEN, CANCELADO ES DE QUIEN PIDIÓ, y son dos cosas
        distintas por mucho que las dos acaben sin mercancía en movimiento: un
        rechazo es la bodega diciendo «no lo atiendo» -y le baja el cumplimiento
        de atención-, una cancelación es el destino retirando su propia petición,
        que no le cuenta a nadie en contra. Por eso van en columnas separadas y
        no sumadas en una de «no salió».
      */}
      <td className="px-3 py-2.5 text-right">
        <Conteo valor={grupo.rechazados} alerta />
      </td>
      <td className="px-3 py-2.5 text-right">
        <Conteo valor={grupo.cancelados} />
      </td>
      <td className="px-3 py-2.5 text-right">
        <Conteo valor={grupo.recibidos} />
      </td>
      <td className="px-3 py-2.5 text-right">
        <Conteo valor={grupo.parciales} alerta />
      </td>
      <td className="px-3 py-2.5 text-right">
        <Conteo valor={grupo.tarde} alerta />
      </td>
      <td className="px-3 py-2.5 text-right">
        <Conteo valor={grupo.novedadesAbiertas} alerta />
      </td>
      <td className="px-3 py-2.5 text-right tabular-nums text-slate-600">
        {grupo.diasTransitoPromedio === null
          ? '—'
          : grupo.diasTransitoPromedio.toLocaleString('es-CO', { maximumFractionDigits: 1 })}
      </td>
      <td className="px-3 py-2.5 text-right">
        <Porcentaje valor={grupo.cumplimientoAtencion} />
      </td>
      <td className="px-3 py-2.5 text-right">
        <Porcentaje valor={grupo.cumplimientoCantidad} />
      </td>
      <td className="px-3 py-2.5 text-right">
        <Porcentaje valor={grupo.cumplimientoPlazo} />
      </td>
    </tr>
  );
}

function Tabla({
  titulo,
  grupos,
  total,
  vacio,
}: {
  titulo: string;
  grupos: CumplimientoGrupoDto[];
  total: CumplimientoGrupoDto | null;
  vacio: string;
}) {
  return (
    <section className="flex flex-col gap-2">
      <h3 className="text-sm font-bold text-slate-900">{titulo}</h3>

      {grupos.length === 0 ? (
        <p className="rounded-xl bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">
          {vacio}
        </p>
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200">
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-sm">
              <thead>
                <tr className="border-b border-petroleo-100 bg-petroleo-50 text-[11px] font-semibold uppercase tracking-wider text-petroleo-800">
                  {ENCABEZADOS.map((h) => (
                    <th key={h.id} scope="col" className={`px-3 py-2.5 ${h.align}`}>
                      {h.texto}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {grupos.map((g) => (
                  <Fila key={g.clave} grupo={g} />
                ))}
                {/* El total va DENTRO de la tabla y no en un recuadro aparte:
                    cada fila solo significa algo comparada con él. */}
                {total === null ? null : <Fila grupo={total} destacada />}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  );
}

interface ReporteCumplimientoProps {
  onCerrar: () => void;
}

/**
 * Cumplimiento logístico de los traslados, por sucursal y por ruta.
 *
 * TODO SON CONTEOS Y PORCENTAJES, ningún volumen. Cada traslado lleva su
 * producto en su unidad -litros, galones, canecas- así que sumar cantidades
 * entre traslados distintos daría una cifra con unidades mezcladas. Lo que sí se
 * puede comparar es cuántos llegaron completos y cuántos a tiempo.
 */
export function ReporteCumplimiento({ onCerrar }: ReporteCumplimientoProps) {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const [desde, setDesde] = useState(haceMeses(3));
  const [hasta, setHasta] = useState(hoyLocal());

  const {
    datos: reporte,
    cargando,
    error,
    esPermisos,
  } = useConsulta(
    () =>
      obtenerReporteCumplimiento(
        sedeActiva,
        // Sin huso y en hora local: la API compara contra `fecha_solicitud`,
        // que es un DATETIME de la tienda. Pasarlo por UTC correría el día.
        desde === '' ? null : `${desde}T00:00:00`,
        hasta === '' ? null : `${hasta}T23:59:59`,
      ),
    [sedeActiva, desde, hasta],
    SIN_REPORTE,
  );

  return (
    <Modal
      titulo="Cumplimiento logístico"
      descripcion={`${nombreSedeActiva} · traslados solicitados en el periodo`}
      ancho="xl"
      onCerrar={onCerrar}
      pie={
        <Boton variante="secundaria" onClick={onCerrar}>
          Cerrar
        </Boton>
      }
    >
      <div className="flex flex-col gap-5">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex min-w-0 flex-col gap-1.5">
            <label htmlFor="cump-desde" className="text-sm font-semibold text-slate-700">
              Desde
            </label>
            <input
              id="cump-desde"
              type="date"
              value={desde}
              onChange={(e) => setDesde(e.target.value)}
              className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm tabular-nums text-slate-900 outline-none transition focus:border-petroleo-500 sm:w-44"
            />
          </div>
          <div className="flex min-w-0 flex-col gap-1.5">
            <label htmlFor="cump-hasta" className="text-sm font-semibold text-slate-700">
              Hasta
            </label>
            <input
              id="cump-hasta"
              type="date"
              value={hasta}
              onChange={(e) => setHasta(e.target.value)}
              className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm tabular-nums text-slate-900 outline-none transition focus:border-petroleo-500 sm:w-44"
            />
          </div>
          <Boton
            variante="fantasma"
            onClick={() => {
              setDesde('');
              setHasta('');
            }}
          >
            Todo el histórico
          </Boton>
        </div>

        {cargando ? <CargandoPanel texto="Calculando…" /> : null}

        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}

        {reporte !== null && !cargando ? (
          <>
            <Tabla
              titulo="Por sucursal que despacha"
              grupos={reporte.porSucursal}
              total={reporte.porSucursal.length > 1 ? reporte.total : null}
              vacio="No hay traslados solicitados en este periodo."
            />

            <Tabla
              titulo="Por ruta"
              grupos={reporte.porRuta}
              total={null}
              vacio="No hay traslados solicitados en este periodo."
            />

            {/*
              LAS TRES COLUMNAS DE PORCENTAJE MIDEN COSAS DISTINTAS y hay que
              decirlo, o se leen como tres intentos de lo mismo. Una
              transportadora puede entregar siempre a tiempo y perder producto en
              cada viaje; una bodega puede despacharlo todo y que nunca llegue.
            */}
            <dl className="grid grid-cols-1 gap-3 rounded-xl bg-slate-50 px-4 py-3 text-xs text-slate-600 sm:grid-cols-3">
              <div>
                <dt className="font-semibold text-slate-800">Atención</dt>
                <dd>
                  De lo que le pidieron, cuánto despachó. Mide a la <strong>bodega</strong>. No
                  cuenta los que retiró quien los pidió.
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-800">Cantidad</dt>
                <dd>
                  De lo que salió, cuánto llegó entero. Mide al <strong>transporte</strong>. Un
                  envío ajustado en origen no cuenta como corto.
                </dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-800">Plazo</dt>
                <dd>
                  De lo que llegó, cuánto llegó dentro de la fecha estimada. Mide al{' '}
                  <strong>transporte</strong> en tiempo.
                </dd>
              </div>
            </dl>

            <p className="text-xs text-slate-500">
              Todo son conteos de traslados, nunca volúmenes: cada uno lleva su producto en su
              unidad, y sumar litros con galones daría un número sin significado.{' '}
              <strong>Rechaz.</strong> los rechazó el origen —eso le baja la atención—;{' '}
              <strong>Cancel.</strong> los retiró quien los pidió, y no le cuentan en contra a
              nadie. «Pend.» son las novedades que siguen esperando desenlace; «Días», el tránsito
              real promedio entre despacho y recepción.
            </p>
          </>
        ) : null}
      </div>
    </Modal>
  );
}
