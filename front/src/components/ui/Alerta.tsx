import { AlertTriangle, Info, ShieldAlert } from 'lucide-react';
import type { ReactNode } from 'react';

export type TipoAlerta = 'error' | 'permisos' | 'info';

const ESTILOS: Record<TipoAlerta, string> = {
  error: 'bg-red-50 text-red-900 ring-red-200',
  permisos: 'bg-amber-50 text-amber-900 ring-amber-200',
  info: 'bg-colorsin-50 text-colorsin-900 ring-colorsin-200',
};

interface AlertaProps {
  tipo?: TipoAlerta;
  children: ReactNode;
}

/**
 * Aviso de error o de contexto.
 *
 * Lleva `role="alert"`, que hace que un lector de pantalla lo anuncie en cuanto
 * aparece. Sin eso, quien no ve la pantalla se queda esperando una respuesta que
 * ya llego.
 */
export function Alerta({ tipo = 'error', children }: AlertaProps) {
  const Icono = tipo === 'permisos' ? ShieldAlert : tipo === 'info' ? Info : AlertTriangle;

  return (
    <div
      role="alert"
      className={`flex items-start gap-3 rounded-xl px-4 py-3 text-sm ring-1 ring-inset ${ESTILOS[tipo]}`}
    >
      <Icono size={18} className="mt-0.5 shrink-0" aria-hidden="true" />
      <div className="leading-relaxed">{children}</div>
    </div>
  );
}
