import { Outlet, useLocation } from 'react-router-dom';
import { SedeProvider } from '../../context/SedeContext';
import { CatalogosProvider } from '../../context/CatalogosContext';
import { Navbar } from './Navbar';
import { Sidebar } from './Sidebar';

/**
 * El nombre de cada ruta. NO SE PINTA: es solo para lectores de pantalla.
 *
 * Aqui habia un encabezado visible, con titulo y una segunda linea que explicaba
 * de que iba el modulo. Se fue: la barra lateral ya dice donde esta uno, y la
 * explicacion era una frase que se lee una vez y despues estorba todos los dias.
 *
 * El `<h1>` se queda oculto, no borrado. Sin ninguno, quien navega con lector de
 * pantalla aterriza en la pagina y no oye en que modulo esta -el titulo del
 * documento es el mismo en todas las rutas-, y saltar por encabezados, que es
 * como se recorre una pagina asi, deja de funcionar.
 */
const TITULOS: Record<string, string> = {
  '/': 'Panel general',
  '/inventario/existencias': 'Existencias',
  '/inventario/lotes': 'Lotes FEFO',
  '/traslados': 'Traslados entre sedes',
  '/compras': 'Compras',
  '/ventas': 'Ventas',
  '/usuarios': 'Usuarios',
};

export function MainLayout() {
  const { pathname } = useLocation();
  const titulo = TITULOS[pathname] ?? 'Colorsín';

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
              <h1 className="sr-only">{titulo}</h1>

              <Outlet />
            </main>
          </div>
        </div>
      </CatalogosProvider>
    </SedeProvider>
  );
}
