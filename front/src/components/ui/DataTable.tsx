import { useState } from 'react';
import type { ReactNode } from 'react';
import { Pagination } from './Pagination';
import { CargandoPanel } from './Spinner';

export type AlineacionColumna = 'izquierda' | 'centro' | 'derecha';

export interface ColumnaTabla<T> {
  /** Identificador estable. Sirve de `key` de React en encabezados y celdas. */
  id: string;
  header: string;
  /**
   * Campo de la fila a mostrar tal cual. `keyof T` y no `string`: asi renombrar
   * un campo del DTO rompe la compilacion aqui en vez de dejar una columna vacia.
   */
  accessor?: keyof T;
  /** Render propio. Tiene prioridad sobre `accessor`. */
  render?: (fila: T, indice: number) => ReactNode;
  align?: AlineacionColumna;
  /** Ancho en clases de Tailwind, por ejemplo `w-32`. */
  ancho?: string;
}

interface DataTableProps<T> {
  columnas: ColumnaTabla<T>[];
  data: T[];
  /**
   * Clave estable de cada fila. OBLIGATORIA a proposito.
   *
   * La alternativa comoda -usar el indice- es un error silencioso: al filtrar,
   * ordenar o cambiar de pagina, React reutiliza el nodo de la posicion 3 para
   * una fila distinta y arrastra el estado interno de la anterior. Con los datos
   * de este sistema todos los DTO traen `id`, asi que el costo es una linea.
   */
  claveFila: (fila: T) => string | number;
  cargando?: boolean;
  estadoVacio?: ReactNode;
  /** `false` muestra la tabla completa, sin pie de paginacion. */
  paginar?: boolean;
  tamanoPaginaInicial?: number;
}

const ALINEACION: Record<AlineacionColumna, string> = {
  izquierda: 'text-left',
  centro: 'text-center',
  derecha: 'text-right',
};

/** Convierte el valor de un `accessor` en algo que React pueda pintar. */
function textoDe(valor: unknown): ReactNode {
  if (valor === null || valor === undefined) {
    return '—';
  }
  if (typeof valor === 'string' || typeof valor === 'number') {
    return valor;
  }
  if (typeof valor === 'boolean') {
    return valor ? 'Sí' : 'No';
  }
  // Un objeto o un arreglo no se pintan solos: `String(...)` da "[object
  // Object]", que al menos es visible en pantalla en vez de reventar el render.
  return String(valor);
}

/**
 * Tabla generica de la aplicacion.
 *
 * LA PAGINACION ES EN MEMORIA, y es la decision correcta HOY: ninguno de los
 * endpoints del backend pagina -devuelven la lista completa con un tope duro- asi
 * que partirla aqui es lo unico que se puede hacer sin inventar un contrato que
 * el servidor no tiene. El dia que una tabla crezca de verdad, lo que cambia es
 * este componente y no las cuatro pantallas que lo usan.
 *
 * LA PAGINA SE ACOTA AL PINTAR, no con un efecto que la reinicie. Si estas en la
 * pagina 5 y un filtro deja tres registros, `paginaSegura` vuelve sola a la 1 sin
 * un render intermedio con la tabla vacia, que es lo que se ve cuando esa
 * correccion se hace dentro de un `useEffect`.
 */
export function DataTable<T>({
  columnas,
  data,
  claveFila,
  cargando = false,
  estadoVacio = 'No hay registros para mostrar.',
  paginar = true,
  tamanoPaginaInicial = 10,
}: DataTableProps<T>) {
  const [pagina, setPagina] = useState(1);
  const [tamanoPagina, setTamanoPagina] = useState(tamanoPaginaInicial);

  const totalRegistros = data.length;
  const totalPaginas = Math.max(1, Math.ceil(totalRegistros / tamanoPagina));
  const paginaSegura = Math.min(pagina, totalPaginas);

  const filas = paginar
    ? data.slice((paginaSegura - 1) * tamanoPagina, paginaSegura * tamanoPagina)
    : data;

  function irA(destino: number) {
    setPagina(Math.min(Math.max(1, destino), totalPaginas));
  }

  function cambiarTamano(nuevo: number) {
    setTamanoPagina(nuevo);
    // Volver al principio: con 50 por pagina la 4 casi nunca existe, y quedarse
    // en ella mostraria una tabla vacia justo despues de pedir ver MAS filas.
    setPagina(1);
  }

  const hayFilas = !cargando && filas.length > 0;
  const vacia = !cargando && filas.length === 0;

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="border-b border-petroleo-100 bg-petroleo-50">
              {columnas.map((columna) => (
                <th
                  key={columna.id}
                  scope="col"
                  className={`px-5 py-3 text-[11px] font-semibold uppercase tracking-wider text-petroleo-800 ${
                    ALINEACION[columna.align ?? 'izquierda']
                  } ${columna.ancho ?? ''}`}
                >
                  {columna.header}
                </th>
              ))}
            </tr>
          </thead>

          <tbody>
            {cargando ? (
              <tr>
                <td colSpan={columnas.length}>
                  <CargandoPanel texto="Cargando registros…" />
                </td>
              </tr>
            ) : null}

            {vacia ? (
              <tr>
                <td
                  colSpan={columnas.length}
                  className="px-5 py-12 text-center text-sm text-slate-500"
                >
                  {estadoVacio}
                </td>
              </tr>
            ) : null}

            {hayFilas
              ? filas.map((fila, indice) => (
                  <tr
                    key={claveFila(fila)}
                    className="border-b border-slate-100 transition last:border-0 even:bg-slate-50/70 hover:bg-petroleo-50/60"
                  >
                    {columnas.map((columna) => (
                      <td
                        key={columna.id}
                        className={`px-5 py-3.5 align-middle ${
                          ALINEACION[columna.align ?? 'izquierda']
                        }`}
                      >
                        {columna.render
                          ? columna.render(fila, indice)
                          : columna.accessor !== undefined
                            ? textoDe(fila[columna.accessor])
                            : null}
                      </td>
                    ))}
                  </tr>
                ))
              : null}
          </tbody>
        </table>
      </div>

      {/* El pie no aparece mientras carga: un "0 a 0 de 0 registros" bajo un
          spinner es ruido que ademas parpadea cuando llegan los datos. */}
      {paginar && !cargando ? (
        <Pagination
          pagina={paginaSegura}
          totalPaginas={totalPaginas}
          totalRegistros={totalRegistros}
          tamanoPagina={tamanoPagina}
          onPagina={irA}
          onTamanoPagina={cambiarTamano}
        />
      ) : null}
    </div>
  );
}
