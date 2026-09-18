import type { ButtonHTMLAttributes, ReactNode } from 'react';

export type VarianteBoton = 'primaria' | 'secundaria' | 'fantasma';

/**
 * TERRACOTA ES EL COLOR DE LAS ACCIONES, y el unico sitio donde aparece como
 * fondo. El verde petroleo viste el chrome -barra superior y lateral- y nunca
 * una accion; asi, en cualquier pantalla, lo naranja es lo que hace algo.
 *
 * El fondo de la variante primaria es `terracota-600` y no `terracota-500`
 * porque el 500 da 4,2:1 contra el texto blanco y no llega al minimo de 4,5:1.
 * El 500 queda para bordes e indicadores, que no llevan letra encima.
 */
const VARIANTES: Record<VarianteBoton, string> = {
  primaria:
    'bg-terracota-600 text-white hover:bg-terracota-700 disabled:bg-slate-300 disabled:text-slate-500',
  secundaria:
    'border border-slate-300 bg-white text-slate-800 hover:bg-slate-50 disabled:text-slate-400',
  fantasma: 'text-slate-600 hover:bg-slate-100 hover:text-slate-900 disabled:text-slate-300',
};

/**
 * `className` se concatena al final para poder ajustar margenes o ancho desde
 * fuera. NO sirve para cambiar lo que ya define la variante: dos utilidades de
 * Tailwind de la misma familia no se resuelven por el orden en que se escriben
 * aqui, sino por su orden en la hoja generada. Si hace falta otro aspecto, se
 * agrega una variante.
 */
interface BotonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: VarianteBoton;
  children: ReactNode;
}

export function Boton({
  variante = 'primaria',
  className = '',
  // Por defecto `button` y no `submit`: un `<button>` sin `type` dentro de un
  // formulario lo envia, y ese es el origen clasico del formulario que se manda
  // solo al pulsar un boton que solo pretendia abrir un panel.
  type = 'button',
  children,
  ...resto
}: BotonProps) {
  return (
    <button
      type={type}
      className={`flex h-11 items-center justify-center gap-2.5 rounded-lg px-4 text-sm font-semibold transition disabled:cursor-not-allowed ${VARIANTES[variante]} ${className}`}
      {...resto}
    >
      {children}
    </button>
  );
}
