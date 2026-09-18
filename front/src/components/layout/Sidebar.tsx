import { NavLink } from 'react-router-dom';
import { Boxes, LayoutDashboard } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { ETIQUETA_ROL } from '../../context/AuthContext';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';

interface ItemNav {
  etiqueta: string;
  ruta: string;
  Icono: LucideIcon;
  /** `end` evita que "/" quede marcado como activo en todas las rutas hijas. */
  exacta?: boolean;
}

/**
 * Solo las dos pantallas que existen.
 *
 * No se listan Compras, Ventas ni Traslados aunque la API ya los expone: un menu
 * con enlaces que no llevan a ninguna parte es peor que un menu corto. Se agregan
 * cuando se construya cada pantalla.
 */
const ITEMS: ItemNav[] = [
  { etiqueta: 'Panel general', ruta: '/', Icono: LayoutDashboard, exacta: true },
  { etiqueta: 'Existencias', ruta: '/inventario/existencias', Icono: Boxes },
];

export function Sidebar() {
  const { sesion, rol } = useAuth();
  const { nombreSedeActiva } = useSede();

  return (
    <aside className="flex w-60 shrink-0 flex-col bg-slate-900 px-3 py-5">
      <div className="flex items-center gap-2.5 px-2 pb-6">
        <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-colorsin-500 text-base font-black text-white">
          C
        </div>
        <span className="text-lg font-bold tracking-tight text-white">Colorsin</span>
      </div>

      <nav aria-label="Secciones" className="flex flex-col gap-1">
        {ITEMS.map(({ etiqueta, ruta, Icono, exacta }) => (
          <NavLink
            key={ruta}
            to={ruta}
            end={exacta}
            className={({ isActive }) =>
              `flex h-11 items-center gap-3 rounded-lg px-3 text-sm transition ${
                isActive
                  ? 'bg-colorsin-700 font-semibold text-white'
                  : 'font-medium text-slate-300 hover:bg-slate-800 hover:text-white'
              }`
            }
          >
            <Icono size={18} aria-hidden="true" />
            <span>{etiqueta}</span>
          </NavLink>
        ))}
      </nav>

      <div className="flex-1" />

      <div className="border-t border-slate-700 px-2 pt-4">
        <div className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
          Sesion
        </div>
        <div className="mt-2 truncate text-sm font-semibold text-white">
          {sesion?.nombre ?? ''}
        </div>
        <div className="truncate text-xs text-slate-400">{rol ? ETIQUETA_ROL[rol] : ''}</div>
        <div className="mt-2.5 truncate text-xs text-slate-400">{nombreSedeActiva}</div>
      </div>
    </aside>
  );
}
