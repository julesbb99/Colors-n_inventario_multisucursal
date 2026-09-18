import { LogOut } from 'lucide-react';
import { ETIQUETA_ROL } from '../../context/AuthContext';
import { useAuth } from '../../hooks/useAuth';
import { SelectorSede } from './SelectorSede';

/** Las iniciales del nombre, para el avatar. */
function iniciales(nombre: string): string {
  const partes = nombre.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) {
    return '??';
  }
  const primera = partes[0].charAt(0);
  const segunda = partes.length > 1 ? partes[1].charAt(0) : '';
  return (primera + segunda).toUpperCase();
}

interface NavbarProps {
  titulo: string;
  subtitulo?: string;
}

export function Navbar({ titulo, subtitulo }: NavbarProps) {
  const { sesion, rol, logout } = useAuth();

  return (
    <header className="flex h-20 shrink-0 items-center gap-4 border-b border-slate-200 bg-white px-6">
      <div className="min-w-0 flex-1">
        <h1 className="truncate text-xl font-bold tracking-tight text-slate-900">{titulo}</h1>
        {subtitulo ? <p className="truncate text-sm text-slate-500">{subtitulo}</p> : null}
      </div>

      <SelectorSede />

      <div className="flex items-center gap-3 border-l border-slate-200 pl-4">
        <div className="hidden text-right sm:block">
          <div className="text-sm font-semibold text-slate-900">{sesion?.nombre ?? ''}</div>
          {/*
            La etiqueta sale del ROL DEL TOKEN, no del campo `rol` que devuelve
            el login: ese trae el texto de la base ('Administrador General') y
            aqui interesa que lo que se muestra corresponda a lo que la API
            realmente compara.
          */}
          <div className="text-xs text-slate-500">{rol ? ETIQUETA_ROL[rol] : ''}</div>
        </div>

        <div
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-colorsin-700 text-sm font-bold text-white"
          aria-hidden="true"
        >
          {iniciales(sesion?.nombre ?? '')}
        </div>

        <button
          type="button"
          onClick={logout}
          title="Cerrar sesion"
          className="flex h-11 w-11 items-center justify-center rounded-lg text-slate-500 transition hover:bg-slate-100 hover:text-slate-800"
        >
          <LogOut size={18} aria-hidden="true" />
          <span className="sr-only">Cerrar sesion</span>
        </button>
      </div>
    </header>
  );
}
