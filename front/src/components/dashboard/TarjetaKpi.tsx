import type { LucideIcon } from 'lucide-react';

export type TonoKpi = 'neutro' | 'alerta' | 'critico';

const TONO_VALOR: Record<TonoKpi, string> = {
  neutro: 'text-slate-900',
  alerta: 'text-amber-700',
  critico: 'text-red-700',
};

const TONO_ICONO: Record<TonoKpi, string> = {
  neutro: 'bg-colorsin-50 text-colorsin-700',
  alerta: 'bg-amber-50 text-amber-700',
  critico: 'bg-red-50 text-red-700',
};

interface TarjetaKpiProps {
  etiqueta: string;
  valor: string;
  /** Linea de contexto: sin ella una cifra suelta no se puede interpretar. */
  detalle?: string;
  Icono: LucideIcon;
  /** `alerta` y `critico` solo se usan cuando el numero PIDE una accion. */
  tono?: TonoKpi;
}

export function TarjetaKpi({
  etiqueta,
  valor,
  detalle,
  Icono,
  tono = 'neutro',
}: TarjetaKpiProps) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5">
      <div className="flex items-center gap-2.5">
        <span
          className={`flex h-8 w-8 items-center justify-center rounded-lg ${TONO_ICONO[tono]}`}
        >
          <Icono size={16} aria-hidden="true" />
        </span>
        <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
          {etiqueta}
        </span>
      </div>

      {/* `tabular-nums` mantiene las cifras alineadas entre tarjetas: sin el,
          los digitos de ancho distinto hacen bailar el bloque al refrescar. */}
      <div className={`mt-3 text-3xl font-bold tabular-nums tracking-tight ${TONO_VALOR[tono]}`}>
        {valor}
      </div>

      {detalle ? <div className="mt-1.5 text-sm text-slate-500">{detalle}</div> : null}
    </div>
  );
}
