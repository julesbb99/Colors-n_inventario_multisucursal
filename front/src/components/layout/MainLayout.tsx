import { Outlet, useLocation } from 'react-router-dom';
import { SedeProvider } from '../../context/SedeContext';
import { CatalogosProvider } from '../../context/CatalogosContext';
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
    // Ya no dice "en unidad base": la tabla permite mirar el mismo saldo en
    // litros, galones o canecas, y el subtitulo contradecia lo que se veia.
    subtitulo: 'Saldo por sede y producto, en la unidad que elijas',
  },
  '/inventario/lotes': {
    titulo: 'Lotes FEFO',
    subtitulo: 'Trazabilidad y caducidad: primero sale el que vence antes',
  },
  '/traslados': {
    titulo: 'Traslados entre sedes',
    subtitulo: 'Lo pide el destino, lo despacha el origen',
  },
  '/compras': {
    titulo: 'Compras',
    subtitulo: 'Órdenes a proveedor y recepción de mercancía',
  },
  '/ventas': {
    titulo: 'Ventas',
    subtitulo: 'Salidas de mostrador, descontadas por FEFO',
  },
  '/usuarios': {
    titulo: 'Usuarios',
    subtitulo: 'Quién da de alta a quién: el administrador a gerentes y operadores, el gerente a operadores de su sede',
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
      <CatalogosProvider>
        <div className="flex h-screen flex-col overflow-hidden bg-slate-50">
          <Navbar />

          <div className="flex min-h-0 flex-1">
            <Sidebar />

            {/* El scroll vive AQUI y no en el `body`: asi la barra superior y la
                lateral quedan fijas y solo se desplaza el contenido, que es lo
                que se espera de un tablero con tablas largas.

                `relative` NO ES DECORATIVO, y sin el aparecia una SEGUNDA barra
                de desplazamiento en toda la aplicacion.

                El motivo: las etiquetas para lectores de pantalla llevan la
                clase `sr-only` de Tailwind, que es `position: absolute`. Un
                elemento absoluto se coloca respecto a su ancestro POSICIONADO
                mas cercano, y aqui no habia ninguno: el marco entero era
                `static`. Asi que su bloque contenedor pasaba a ser el `<html>`,
                y con el dos consecuencias:

                  1. dejaba de moverse con el scroll de <main>, quedandose
                     clavado a 2.300 px del inicio del DOCUMENTO;
                  2. el `overflow-hidden` del contenedor de arriba NO lo
                     recortaba, porque solo recorta lo que cuelga de el en la
                     cadena de bloques contenedores.

                Resultado: el documento medi­a 2.362 px de alto aunque todo
                cupiera en la pantalla, y el navegador pintaba su propia barra
                al lado de la de <main>. Esa barra se desplazaba 1.594 px sobre
                nada, que es lo que se veia al final de la pagina.

                Con `relative`, esas etiquetas se anclan a <main> y vuelven a
                comportarse como parte del contenido. Los modales no se ven
                afectados: usan `fixed`, al que un ancestro `relative` no le
                cambia nada. */}
            <main className="relative min-w-0 flex-1 overflow-y-auto bg-slate-50 px-6 py-5">
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
      </CatalogosProvider>
    </SedeProvider>
  );
}
