import type { ReactNode } from 'react';
import type { EstadoCaducidad } from '../../models/inventario';

export type TonoBadge = 'verde' | 'amarillo' | 'rojo' | 'azul' | 'neutro';

/**
 * Las clases de cada tono.
 *
 * Van escritas COMPLETAS y no compuestas con plantillas (`bg-${color}-100`)
 * porque Tailwind analiza el codigo como texto: una clase construida en tiempo
 * de ejecucion no aparece en el archivo generado y el color sale en blanco.
 *
 * Los tonos de texto son los 800 y no los 600 a proposito: sobre un fondo 100,
 * el 600 no llega al contraste 4.5:1 que necesita un texto de 12 px.
 */
const TONOS: Record<TonoBadge, string> = {
  verde: 'bg-emerald-100 text-emerald-800 ring-emerald-200',
  amarillo: 'bg-amber-100 text-amber-800 ring-amber-200',
  rojo: 'bg-red-100 text-red-800 ring-red-200',
  azul: 'bg-colorsin-100 text-colorsin-800 ring-colorsin-200',
  neutro: 'bg-slate-100 text-slate-700 ring-slate-200',
};

interface BadgeProps {
  tono: TonoBadge;
  children: ReactNode;
}

export function Badge({ tono, children }: BadgeProps) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset ${TONOS[tono]}`}
    >
      {children}
    </span>
  );
}

/** El tono que le corresponde a cada estado de caducidad. */
const TONO_CADUCIDAD: Record<EstadoCaducidad, TonoBadge> = {
  Vigente: 'verde',
  'Por vencer': 'amarillo',
  Vencido: 'rojo',
};

export function BadgeCaducidad({ estado }: { estado: EstadoCaducidad }) {
  return <Badge tono={TONO_CADUCIDAD[estado]}>{estado}</Badge>;
}
