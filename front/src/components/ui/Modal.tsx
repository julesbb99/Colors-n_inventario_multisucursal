import { useEffect, useId, useRef } from 'react';
import type { ReactNode } from 'react';
import { X } from 'lucide-react';

interface ModalProps {
  titulo: string;
  descripcion?: string;
  /** El pie: botones de acción. Va aparte para que quede fijo al desplazar el cuerpo. */
  pie?: ReactNode;
  children: ReactNode;
  onCerrar: () => void;
  /** `true` mientras se envía: bloquea el cierre para no abandonar a mitad. */
  ocupado?: boolean;
  ancho?: 'md' | 'lg' | 'xl';
}

const ANCHOS: Record<'md' | 'lg' | 'xl', string> = {
  md: 'max-w-lg',
  lg: 'max-w-2xl',
  xl: 'max-w-4xl',
};

/**
 * Ventana modal.
 *
 * NO SE CIERRA CON CLIC EN EL FONDO. En un formulario con varias líneas
 * escritas, un clic fuera por descuido borraría todo sin preguntar; cerrar exige
 * la X o Cancelar, que son deliberados. Escape sí cierra, porque es un gesto
 * consciente, pero no mientras se está enviando.
 */
export function Modal({
  titulo,
  descripcion,
  pie,
  children,
  onCerrar,
  ocupado = false,
  ancho = 'lg',
}: ModalProps) {
  const idTitulo = useId();
  const contenedor = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const alTeclear = (evento: KeyboardEvent) => {
      if (evento.key === 'Escape' && !ocupado) {
        onCerrar();
      }
    };

    document.addEventListener('keydown', alTeclear);
    // El fondo no debe desplazarse detrás del modal.
    const overflowPrevio = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    contenedor.current?.focus();

    return () => {
      document.removeEventListener('keydown', alTeclear);
      document.body.style.overflow = overflowPrevio;
    };
  }, [onCerrar, ocupado]);

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-petroleo-900/50 p-4 sm:p-8">
      <div
        ref={contenedor}
        role="dialog"
        aria-modal="true"
        aria-labelledby={idTitulo}
        tabIndex={-1}
        className={`w-full ${ANCHOS[ancho]} rounded-2xl bg-white shadow-xl outline-none`}
      >
        <div className="flex items-start gap-4 border-b border-slate-200 px-6 py-4">
          <div className="min-w-0 flex-1">
            <h2 id={idTitulo} className="text-lg font-bold text-slate-900">
              {titulo}
            </h2>
            {descripcion ? (
              <p className="mt-0.5 text-sm text-slate-500">{descripcion}</p>
            ) : null}
          </div>

          <button
            type="button"
            onClick={onCerrar}
            disabled={ocupado}
            className="-mr-2 -mt-1 flex h-10 w-10 shrink-0 items-center justify-center rounded-lg text-slate-500 transition hover:bg-slate-100 hover:text-slate-900 disabled:opacity-40"
          >
            <X size={18} aria-hidden="true" />
            <span className="sr-only">Cerrar</span>
          </button>
        </div>

        <div className="px-6 py-5">{children}</div>

        {pie ? (
          <div className="flex flex-wrap items-center justify-end gap-3 border-t border-slate-200 bg-slate-50 px-6 py-4">
            {pie}
          </div>
        ) : null}
      </div>
    </div>
  );
}
