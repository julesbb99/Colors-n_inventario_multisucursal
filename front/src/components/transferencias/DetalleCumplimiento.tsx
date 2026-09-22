import { ArrowRight } from 'lucide-react';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerDetalleCumplimiento } from '../../services/transferencias';
import {
  ETIQUETA_ESTADO_TRANSFERENCIA,
  ETIQUETA_PLAZO,
  ETIQUETA_TIPO_NOVEDAD,
  ETIQUETA_TRATAMIENTO,
} from '../../models/transferencias';
import type {
  CumplimientoDetalleDto,
  CumplimientoTrasladoDto,
  EstadoPlazo,
} from '../../models/transferencias';
import { Alerta } from '../ui/Alerta';
import { ChipColor } from '../ui/ChipColor';
import { CargandoPanel } from '../ui/Spinner';
import { StatusBadge } from '../ui/StatusBadge';
import type { EstadoBadge } from '../ui/StatusBadge';
import { formatearEntero, formatearFechaSolo, formatearVolumen } from '../../utils/formato';

const SIN_DATOS: CumplimientoDetalleDto | null = null;

/**
 * El tono de cada desenlace de plazo.
 *
 * «Sin plazo» va en ámbar y no en gris: no es que dé igual, es que no se puede
 * juzgar, y eso es un hueco en los datos que conviene que se vea. Los dos
 * estados vivos —sin despachar, en camino— van neutros porque todavía no han
 * incumplido nada.
 */
const TONO_PLAZO: Record<EstadoPlazo, EstadoBadge> = {
  ATiempo: 'activo',
  Tarde: 'critico',
  EnTransito: 'advertencia',
  SinDespachar: 'neutral',
  SinPlazo: 'por_vencer',
  NoAplica: 'inactivo',
};

/**
 * Los días de desviación, con su signo leído en palabras.
 *
 * NO SE PINTA EL NÚMERO A SECAS. Un «−2» en una columna llamada «desviación» se
 * lee mal la mitad de las veces: puede entenderse como dos días de retraso. Con
 * «2 d antes» y «2 d tarde» no hay forma de confundirlo.
 */
function Desviacion({ dias }: { dias: number | null }) {
  if (dias === null) {
    return <span className="text-slate-400">—</span>;
  }
  if (dias === 0) {
    return <span className="tabular-nums text-emerald-700">el día previsto</span>;
  }
  return dias < 0 ? (
    <span className="whitespace-nowrap tabular-nums text-emerald-700">
      {formatearEntero(-dias)} d antes
    </span>
  ) : (
    <span className="whitespace-nowrap font-semibold tabular-nums text-terracota-700">
      {formatearEntero(dias)} d tarde
    </span>
  );
}

function Cantidad({
  valor,
  unidad,
  alerta = false,
}: {
  valor: number | null;
  unidad: string;
  alerta?: boolean;
}) {
  return (
    <span
      className={`whitespace-nowrap tabular-nums ${
        valor === null
          ? 'text-slate-400'
          : alerta
            ? 'font-semibold text-terracota-700'
            : 'text-slate-700'
      }`}
    >
      {formatearVolumen(valor, unidad)}
    </span>
  );
}

function Fila({ t }: { t: CumplimientoTrasladoDto }) {
  return (
    <tr className="border-b border-slate-100 align-top last:border-0 even:bg-slate-50/70">
      <td className="px-3 py-2.5">
        <span className="font-semibold tabular-nums text-slate-900">{t.id}</span>
        <span className="block whitespace-nowrap text-[11px] text-slate-500">
          {formatearFechaSolo(t.fechaSolicitud)}
        </span>
      </td>

      <td className="px-3 py-2.5">
        <span className="flex items-center gap-1.5 whitespace-nowrap text-slate-700">
          <span>{t.sucursalOrigenNombre}</span>
          <ArrowRight size={13} className="shrink-0 text-terracota-600" aria-hidden="true" />
          <span>{t.sucursalDestinoNombre}</span>
        </span>
        {/* La transportadora y la GUÍA van aquí, pegadas a la ruta: son con lo
            que se reclama, y en un informe de cumplimiento eso no es un adorno
            sino el dato que permite actuar sobre la fila. */}
        <span className="block text-[11px] text-slate-500">
          {t.transportadoraNombre ?? 'sin transportadora'}
          {t.guia === null ? '' : ` · guía ${t.guia}`}
        </span>
      </td>

      <td className="px-3 py-2.5">
        <span className="flex items-start gap-2">
          <ChipColor nombre={t.productoNombre} alto={22} />
          <span className="min-w-0">
            <span className="block text-slate-800">{t.productoNombre}</span>
            {t.lotes.length === 0 ? (
              <span className="block text-[11px] text-slate-400">sin lotes</span>
            ) : (
              <span className="mt-0.5 flex flex-wrap gap-1">
                {t.lotes.map((l, i) => (
                  <span
                    key={l.loteId ?? `sin-lote-${i}`}
                    // La cantidad va en unidad BASE, no en la del traslado: 5
                    // galones son 18,93 litros de lote.
                    title={
                      `${formatearVolumen(l.cantidadBase, t.unidadBaseSimbolo)} de este lote` +
                      (l.fechaVencimiento === null
                        ? ''
                        : ` · vence el ${formatearFechaSolo(l.fechaVencimiento)}`)
                    }
                    className={`inline-flex whitespace-nowrap rounded px-1.5 py-0.5 font-mono text-[11px] ${
                      l.numeroLote === null
                        ? 'bg-amber-50 text-amber-800 ring-1 ring-inset ring-amber-200'
                        : 'bg-slate-100 text-slate-600'
                    }`}
                  >
                    {l.numeroLote ?? 'sin lote'}
                  </span>
                ))}
              </span>
            )}
          </span>
        </span>
      </td>

      <td className="px-3 py-2.5 text-right">
        <Cantidad valor={t.cantidadSolicitada} unidad={t.unidadSimbolo} />
      </td>
      <td className="px-3 py-2.5 text-right">
        {/* Ámbar cuando el origen ajustó: no es una pérdida, es que no tenía
            todo. El rojo se reserva para lo que se perdió en el camino. */}
        <span
          className={`whitespace-nowrap tabular-nums ${
            t.ajustadoEnOrigen ? 'font-semibold text-amber-700' : 'text-slate-700'
          }`}
          title={t.ajustadoEnOrigen ? 'El origen no tenía todo lo pedido.' : undefined}
        >
          {formatearVolumen(t.cantidadDespachada, t.unidadSimbolo)}
        </span>
      </td>
      <td className="px-3 py-2.5 text-right">
        <Cantidad
          valor={t.cantidadRecibida}
          unidad={t.unidadSimbolo}
          alerta={t.llegoCompleto === false}
        />
      </td>

      <td className="px-3 py-2.5 text-right">
        <span className="whitespace-nowrap tabular-nums text-slate-600">
          {t.diasTransito === null
            ? '—'
            : `${t.diasTransito.toLocaleString('es-CO', { maximumFractionDigits: 1 })} d`}
        </span>
        <span className="block whitespace-nowrap text-[11px] text-slate-400">
          {t.fechaEstimadaLlegada === null
            ? 'sin previsión'
            : `previsto ${formatearFechaSolo(t.fechaEstimadaLlegada)}`}
        </span>
      </td>

      <td className="px-3 py-2.5 text-center">
        <StatusBadge estado={TONO_PLAZO[t.plazo]}>{ETIQUETA_PLAZO[t.plazo]}</StatusBadge>
        <span className="mt-0.5 block text-[11px]">
          <Desviacion dias={t.diasDesviacion} />
        </span>
      </td>

      <td className="px-3 py-2.5">
        {t.novedades.length === 0 ? (
          <span className="text-slate-400">—</span>
        ) : (
          <span className="flex flex-col gap-1">
            {t.novedades.map((n, i) => {
              const abierta = n.estado === 'Abierta';
              return (
                <span
                  key={i}
                  title={
                    abierta
                      ? `Pendiente: ${ETIQUETA_TRATAMIENTO[n.tratamiento]}`
                      : `Cerrada · ${ETIQUETA_TRATAMIENTO[n.tratamiento]}`
                  }
                  className={`inline-flex w-fit items-center gap-1.5 whitespace-nowrap rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                    abierta
                      ? 'bg-terracota-100 text-terracota-800 ring-1 ring-inset ring-terracota-300'
                      : 'bg-slate-100 text-slate-600'
                  }`}
                >
                  {abierta ? (
                    <span
                      aria-hidden="true"
                      className="h-1.5 w-1.5 shrink-0 rounded-full bg-terracota-600"
                    />
                  ) : null}
                  {n.tipo === null ? 'Novedad' : ETIQUETA_TIPO_NOVEDAD[n.tipo]}
                  {n.cantidadAfectada === null
                    ? ''
                    : ` ${formatearVolumen(n.cantidadAfectada, t.unidadSimbolo)}`}
                </span>
              );
            })}
          </span>
        )}
      </td>

      <td className="px-3 py-2.5 text-center">
        {t.estado === null ? (
          <StatusBadge estado="neutral" />
        ) : (
          <StatusBadge
            estado={
              t.estado === 'Completada'
                ? 'exitoso'
                : t.estado === 'Rechazada'
                  ? 'critico'
                  : t.estado === 'Cancelada' || t.estado === 'Cerrada'
                    ? 'inactivo'
                    : 'advertencia'
            }
          >
            {ETIQUETA_ESTADO_TRANSFERENCIA[t.estado]}
          </StatusBadge>
        )}
      </td>
    </tr>
  );
}

interface DetalleCumplimientoProps {
  sedeActiva: number | null;
  /** `AAAA-MM-DD` o cadena vacía. Los mismos que usa el resumen. */
  desde: string;
  hasta: string;
}

/**
 * El informe de cumplimiento TRASLADO POR TRASLADO.
 *
 * EL RESUMEN CONTESTA «CÓMO VAMOS»; ESTE CONTESTA «CUÁL FALLÓ». Un 66 % de
 * cumplimiento de plazo no dice qué traslado llegó tarde, con qué
 * transportadora ni con qué guía, y esos tres datos son los que hacen falta
 * para reclamarle a alguien.
 *
 * VA EN SU PROPIA PESTAÑA Y PIDE SUS PROPIOS DATOS. El resumen son cuatro filas
 * agregadas y se abre siempre; esto es una fila por traslado con sus lotes y sus
 * novedades, y solo se pide cuando alguien lo mira. Juntarlos en una consulta
 * haría esperar al caso frecuente por el caso raro.
 */
export function DetalleCumplimiento({ sedeActiva, desde, hasta }: DetalleCumplimientoProps) {
  const { datos, cargando, error, esPermisos } = useConsulta(
    () =>
      obtenerDetalleCumplimiento(
        sedeActiva,
        // Sin huso y en hora local, igual que el resumen: la API compara contra
        // `fecha_solicitud`, que es un DATETIME de la tienda.
        desde === '' ? null : `${desde}T00:00:00`,
        hasta === '' ? null : `${hasta}T23:59:59`,
      ),
    [sedeActiva, desde, hasta],
    SIN_DATOS,
  );

  if (cargando) {
    return <CargandoPanel texto="Cargando los traslados…" />;
  }

  if (error) {
    return <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>;
  }

  if (datos === null || datos.traslados.length === 0) {
    return (
      <p className="rounded-xl bg-slate-50 px-4 py-8 text-center text-sm text-slate-500">
        No hay traslados solicitados en este periodo.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="overflow-hidden rounded-xl border border-slate-200">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-petroleo-100 bg-petroleo-50 text-[11px] font-semibold uppercase tracking-wider text-petroleo-800">
                <th scope="col" className="px-3 py-2.5 text-left">Nº</th>
                <th scope="col" className="px-3 py-2.5 text-left">Ruta y transporte</th>
                <th scope="col" className="px-3 py-2.5 text-left">Producto y lotes</th>
                <th scope="col" className="px-3 py-2.5 text-right">Pedido</th>
                <th scope="col" className="px-3 py-2.5 text-right">Despachado</th>
                <th scope="col" className="px-3 py-2.5 text-right">Recibido</th>
                <th scope="col" className="px-3 py-2.5 text-right">Tránsito</th>
                <th scope="col" className="px-3 py-2.5 text-center">Plazo</th>
                <th scope="col" className="px-3 py-2.5 text-left">Novedades</th>
                <th scope="col" className="px-3 py-2.5 text-center">Estado</th>
              </tr>
            </thead>
            <tbody>
              {datos.traslados.map((t) => (
                <Fila key={t.id} t={t} />
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Se avisa cuando el tope dejó filas fuera, y se dice qué hacer. Una
          lista recortada en silencio se lee como «esto es todo lo que hay». */}
      {datos.hayMas ? (
        <Alerta tipo="info">
          Se muestran los {formatearEntero(datos.limite)} más recientes, que es el tope de la
          consulta. Hay más traslados en este periodo: acótelo con las fechas de arriba para
          verlos.
        </Alerta>
      ) : null}

      <p className="text-xs text-slate-500">
        <strong>Tránsito</strong> son los días reales entre despacho y recepción;{' '}
        <strong>Plazo</strong> los compara con la fecha prevista. «Sin plazo» son los despachados
        antes de que esa fecha fuera obligatoria: no se pueden juzgar, y por eso no cuentan ni a
        tiempo ni tarde en los porcentajes del resumen. <strong>Recibido</strong> se compara
        contra lo despachado, no contra lo pedido: si el origen ajustó el envío —en ámbar— y llegó
        todo lo que salió, el traslado llegó completo.
      </p>
    </div>
  );
}
