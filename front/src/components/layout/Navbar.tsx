import { LogOut } from 'lucide-react';
import { ETIQUETA_ROL } from '../../context/AuthContext';
import { useAuth } from '../../hooks/useAuth';
import { SelectorSede } from './SelectorSede';

/** Las iniciales del nombre, para el avatar. */
function iniciales(nombre: string): string {
  const partes = nombre.trim().split(/\s+/).filter(Boolean);

  if (partes.length === 0) {
    return '··';
  }

  const primera = partes[0].charAt(0);
  const segunda = partes.length > 1 ? partes[1].charAt(0) : '';

  return (primera + segunda).toUpperCase();
}

/**
 * Barra superior: marca, sede activa y sesion.
 *
 * Ocupa el ancho completo y la lateral cuelga debajo, en vez de ir la marca en
 * la lateral: asi el selector de sede -que condiciona TODO lo que se ve en
 * pantalla- queda a la altura de los ojos y separado de la navegacion, que es
 * una decision distinta.
 *
 * No recibe el titulo de la pantalla. Ese vive en el area de contenido como el
 * unico `h1` de la pagina; ponerlo aqui obligaria a competir con la marca por el
 * mismo espacio y dejaria dos titulos de rango parecido en la misma franja.
 */
export function Navbar() {
  const { sesion, rol, logout } = useAuth();

  return (
    <header className="flex h-16 shrink-0 items-center gap-4 bg-petroleo-900 px-4 sm:px-6">
      <div className="flex shrink-0 items-center gap-2.5">
        <div
          className="flex h-9 w-9 items-center justify-center rounded-xl bg-terracota-600 text-lg font-black leading-none text-white"
          aria-hidden="true"
        >
          C
        </div>
        <span className="hidden text-base font-bold tracking-tight text-white sm:inline">
          Colorsín Industrial S.A.S.
        </span>
      </div>

      <div className="min-w-0 flex-1" />

      <SelectorSede />

      <div className="flex items-center gap-3 border-l border-white/15 pl-3 sm:pl-4">
        <div className="hidden text-right md:block">
          <div className="truncate text-sm font-semibold leading-tight text-white">
            {sesion?.nombre ?? ''}
          </div>
          {/*
            La etiqueta sale del ROL DEL TOKEN, no del campo `rol` que devuelve el
            login: ese trae el texto de la base ('Administrador General') y aqui
            interesa mostrar lo que la API realmente compara.
          */}
          <div className="truncate text-xs leading-tight text-petroleo-200">
            {rol ? ETIQUETA_ROL[rol] : ''}
          </div>
        </div>

        <div
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-petroleo-700 text-xs font-bold text-white ring-1 ring-white/20"
          aria-hidden="true"
        >
          {iniciales(sesion?.nombre ?? '')}
        </div>

        <button
          type="button"
          onClick={logout}
          title="Cerrar sesión"
          className="flex h-10 w-10 items-center justify-center rounded-lg text-petroleo-200 transition hover:bg-white/10 hover:text-white"
        >
          <LogOut size={17} aria-hidden="true" />
          <span className="sr-only">Cerrar sesión</span>
        </button>
      </div>
    </header>
  );
}
