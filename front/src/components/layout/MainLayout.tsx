import { Outlet, useLocation } from 'react-router-dom';
import { SedeProvider } from '../../context/SedeContext';
import { Navbar } from './Navbar';
import { Sidebar } from './Sidebar';

interface Encabezado {
  titulo: string;
  subtitulo: string;
}

/**
 * El encabezado de cada ruta.
 *
 * Vive aqui y no dentro de cada pantalla para que el titulo sea parte del marco:
 * si cada pagina pintara el suyo, dos de ellas terminarian con espaciados
 * distintos y el contenido bailaria al navegar.
 */
const ENCABEZADOS: Record<string, Encabezado> = {
  '/': {
    titulo: 'Panel general',
    subtitulo: 'Ventas, inventario y alertas de la sede seleccionada',
  },
  '/inventario/existencias': {
    titulo: 'Existencias',
    subtitulo: 'Saldo por sede y producto, en unidad base',
  },
};

const POR_DEFECTO: Encabezado = { titulo: 'Colorsín', subtitulo: '' };

export function MainLayout() {
  const { pathname } = useLocation();
  const encabezado = ENCABEZADOS[pathname] ?? POR_DEFECTO;

  return (
    // El proveedor de sede va AQUI y no en la raiz: carga el catalogo de
    // sucursales, que exige token. Colgado mas arriba se ejecutaria tambien en
    // /login y responderia 401 antes de que nadie haya entrado.
    <SedeProvider>
      <div className="flex h-screen flex-col overflow-hidden bg-slate-50">
        <Navbar />

        <div className="flex min-h-0 flex-1">
          <Sidebar />

          {/* El scroll vive AQUI y no en el `body`: asi la barra superior y la
              lateral quedan fijas y solo se desplaza el contenido, que es lo que
              se espera de un tablero con tablas largas. */}
          <main className="min-w-0 flex-1 overflow-y-auto bg-slate-50 px-6 py-5">
            <div className="mb-5">
              <h1 className="text-xl font-bold tracking-tight text-slate-900">
                {encabezado.titulo}
              </h1>
              {encabezado.subtitulo ? (
                <p className="mt-0.5 text-sm text-slate-500">{encabezado.subtitulo}</p>
              ) : null}
            </div>

            <Outlet />
          </main>
        </div>
      </div>
    </SedeProvider>
  );
}
