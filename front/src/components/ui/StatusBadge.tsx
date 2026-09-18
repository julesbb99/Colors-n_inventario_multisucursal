import type { ReactNode } from 'react';

/**
 * Los estados que sabe pintar la aplicacion.
 *
 * Vienen en parejas de sinonimo porque los modulos nombran lo mismo de formas
 * distintas -un lote esta 'vencido', una orden esta 'critico'- y obligar a
 * traducirlo en cada tabla es como una termina usando el color equivocado. Cada
 * pareja comparte tono; lo unico que cambia es la etiqueta por defecto.
 */
export type EstadoBadge =
  | 'exitoso'
  | 'activo'
  | 'advertencia'
  | 'por_vencer'
  | 'critico'
  | 'vencido'
  | 'inactivo'
  | 'neutral';

type TonoBadge = 'verde' | 'ambar' | 'terracota' | 'gris';

/**
 * Las clases de cada tono, escritas COMPLETAS.
 *
 * Nunca compuestas con plantillas (`bg-${tono}-50`): Tailwind analiza el codigo
 * como texto, asi que una clase armada en tiempo de ejecucion no llega al CSS
 * generado y el badge sale transparente.
 *
 * El texto va en el 700/800 y no en el 600 porque sobre un fondo 50 el 600 no
 * alcanza el 4,5:1 que necesita una letra de 12 px.
 */
const TONOS: Record<TonoBadge, string> = {
  verde: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  ambar: 'bg-amber-50 text-amber-800 ring-amber-200',
  terracota: 'bg-terracota-50 text-terracota-700 ring-terracota-200',
  gris: 'bg-slate-100 text-slate-700 ring-slate-300',
};

const TONO_DE: Record<EstadoBadge, TonoBadge> = {
  exitoso: 'verde',
  activo: 'verde',
  advertencia: 'ambar',
  por_vencer: 'ambar',
  critico: 'terracota',
  vencido: 'terracota',
  inactivo: 'gris',
  neutral: 'gris',
};

/** Etiqueta por defecto. Se usa cuando el llamador no pasa un texto propio. */
const ETIQUETA_DE: Record<EstadoBadge, string> = {
  exitoso: 'Completado',
  activo: 'Activo',
  advertencia: 'Advertencia',
  por_vencer: 'Por vencer',
  critico: 'Crítico',
  vencido: 'Vencido',
  inactivo: 'Inactivo',
  neutral: 'Sin estado',
};

interface StatusBadgeProps {
  estado: EstadoBadge;
  /** Texto propio. Omitido, usa la etiqueta por defecto del estado. */
  children?: ReactNode;
}

/**
 * La marca de estado de toda la aplicacion.
 *
 * EL COLOR NO ES DECORACION: verde es que algo esta bien, ambar que hay que
 * mirarlo, terracota que hay que actuar, gris que no aplica. Por eso el estado
 * entra como un valor cerrado y no como un color suelto: asi dos modulos no
 * pueden pintar de rojo cosas que significan distinto.
 *
 * El anillo es de 1 px hacia adentro (`ring-inset`) para que la pastilla no
 * crezca dentro de una celda de tabla ni desalinee la fila.
 */
export function StatusBadge({ estado, children }: StatusBadgeProps) {
  return (
    <span
      className={`inline-flex items-center whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset ${TONOS[TONO_DE[estado]]}`}
    >
      {children ?? ETIQUETA_DE[estado]}
    </span>
  );
}
