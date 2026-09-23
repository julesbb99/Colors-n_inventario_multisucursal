import { NavLink } from 'react-router-dom';
import {
  ArrowLeftRight,
  Boxes,
  Layers,
  LayoutDashboard,
  Receipt,
  ShoppingCart,
  Users,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';

interface ItemNav {
  etiqueta: string;
  ruta: string;
  Icono: LucideIcon;
  /** `end` evita que "/" quede marcado como activo en todas las rutas hijas. */
  exacta?: boolean;
  /**
   * Se oculta al operador. NO es el control: `ProtectedRoute` desvía la ruta y
   * la API responde 403. Esconder el enlace solo evita ofrecer una pantalla que
   * daría error nada más abrirla.
   */
  soloSupervision?: boolean;
}

// Aqui habia un octavo elemento, «Configuración», sin pantalla detras y marcado
// «pronto». Se fue porque no va a haberla: un menu que anuncia algo que no
// llegara no informa del alcance del sistema, solo deja esperando. Con el se fue
// tambien la rama que pintaba elementos deshabilitados, que era la unica que la
// usaba.
const ITEMS: ItemNav[] = [
  { etiqueta: 'Dashboard', ruta: '/', Icono: LayoutDashboard, exacta: true },
  { etiqueta: 'Existencias', ruta: '/inventario/existencias', Icono: Boxes },
  { etiqueta: 'Lotes FEFO', ruta: '/inventario/lotes', Icono: Layers },
  { etiqueta: 'Traslados', ruta: '/traslados', Icono: ArrowLeftRight },
  { etiqueta: 'Compras', ruta: '/compras', Icono: ShoppingCart },
  { etiqueta: 'Ventas', ruta: '/ventas', Icono: Receipt },
  { etiqueta: 'Usuarios', ruta: '/usuarios', Icono: Users, soloSupervision: true },
];

/**
 * Clases comunes a todos los elementos del menu.
 *
 * El borde izquierdo va SIEMPRE, transparente cuando el elemento no esta activo:
 * si solo lo llevara el activo, la fila se desplazaria cuatro pixeles al
 * seleccionarla y el menu entero parpadearia en cada navegacion.
 */
const BASE_ITEM =
  'flex h-11 items-center gap-3 border-l-4 pl-3 pr-3 text-sm transition';

export function Sidebar() {
  const { esSupervision } = useAuth();

  const visibles = ITEMS.filter((item) => !item.soloSupervision || esSupervision);

  return (
    <aside className="flex w-56 shrink-0 flex-col bg-petroleo-800 py-4">
      <nav aria-label="Secciones" className="flex flex-col gap-0.5">
        {visibles.map(({ etiqueta, ruta, Icono, exacta }) => (
          <NavLink
            key={etiqueta}
            to={ruta}
            end={exacta}
            className={({ isActive }) =>
              `${BASE_ITEM} ${
                isActive
                  ? 'border-terracota-500 bg-petroleo-700 font-semibold text-white'
                  : 'border-transparent font-medium text-petroleo-100 hover:bg-petroleo-700/60 hover:text-white'
              }`
            }
          >
            <Icono size={18} aria-hidden="true" />
            <span className="flex-1">{etiqueta}</span>
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
