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
 * Vive aqui y no dentro de cada pantalla para que la cabecera sea parte del
 * marco: si cada pagina pintara su propia Navbar, dos de ellas podrian terminar
 * con espaciados distintos y el selector de sede se desalinearia al navegar.
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

const POR_DEFECTO: Encabezado = { titulo: 'Colorsin', subtitulo: '' };

export function MainLayout() {
  const { pathname } = useLocation();
  const encabezado = ENCABEZADOS[pathname] ?? POR_DEFECTO;

  return (
    // El proveedor de sede va AQUI y no en la raiz: carga el catalogo de
    // sucursales, que exige token. Colgado mas arriba se ejecutaria tambien en
    // /login y respondería 401 antes de que nadie haya entrado.
    <SedeProvider>
      <div className="flex h-screen overflow-hidden bg-slate-100">
        <Sidebar />

        <div className="flex min-w-0 flex-1 flex-col">
          <Navbar titulo={encabezado.titulo} subtitulo={encabezado.subtitulo} />

          <main className="flex-1 overflow-y-auto p-6">
            <Outlet />
          </main>
        </div>
      </div>
    </SedeProvider>
  );
}
