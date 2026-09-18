import { Loader2 } from 'lucide-react';

interface SpinnerProps {
  /** Tamano en pixeles del icono. */
  tamano?: number;
  className?: string;
  /** Texto para lectores de pantalla. Una animacion sin etiqueta no dice nada. */
  etiqueta?: string;
}

export function Spinner({ tamano = 18, className = '', etiqueta = 'Cargando' }: SpinnerProps) {
  return (
    <>
      <Loader2 size={tamano} className={`animate-spin ${className}`} aria-hidden="true" />
      <span className="sr-only">{etiqueta}</span>
    </>
  );
}

/** Bloque de carga centrado, para el cuerpo de una pantalla. */
export function CargandoPanel({ texto = 'Cargando…' }: { texto?: string }) {
  return (
    <div
      className="flex items-center justify-center gap-3 py-16 text-slate-500"
      role="status"
      aria-live="polite"
    >
      <Spinner tamano={22} etiqueta={texto} />
      <span className="text-sm">{texto}</span>
    </div>
  );
}
