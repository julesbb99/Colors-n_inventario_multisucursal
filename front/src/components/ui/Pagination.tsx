import { ChevronLeft, ChevronRight } from 'lucide-react';

/** Tamaños de página que ofrece el selector. */
export const TAMANOS_PAGINA = [10, 20, 50] as const;

interface PaginationProps {
  /** Página actual, empezando en 1. */
  pagina: number;
  totalPaginas: number;
  totalRegistros: number;
  tamanoPagina: number;
  onPagina: (pagina: number) => void;
  onTamanoPagina: (tamano: number) => void;
  opcionesTamano?: readonly number[];
}

const BOTON =
  'flex h-9 w-9 items-center justify-center rounded-lg border border-slate-300 bg-white text-slate-600 transition hover:bg-slate-50 hover:text-slate-900 disabled:cursor-not-allowed disabled:border-slate-200 disabled:bg-slate-50 disabled:text-slate-300';

/**
 * Pie de navegación de una tabla.
 *
 * ES UN COMPONENTE CONTROLADO: no guarda la página, la recibe. Quien la guarda
 * es la tabla, que es la única que sabe cuántos registros hay; con el estado
 * aquí adentro, un cambio de filtro dejaría el pie diciendo "página 4 de 1".
 *
 * Los botones se deshabilitan en los extremos en vez de ocultarse: un control
 * que desaparece mueve el resto de la fila y obliga a buscarlo de nuevo.
 */
export function Pagination({
  pagina,
  totalPaginas,
  totalRegistros,
  tamanoPagina,
  onPagina,
  onTamanoPagina,
  opcionesTamano = TAMANOS_PAGINA,
}: PaginationProps) {
  const primero = totalRegistros === 0 ? 0 : (pagina - 1) * tamanoPagina + 1;
  const ultimo = Math.min(pagina * tamanoPagina, totalRegistros);

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-3 border-t border-slate-200 bg-slate-50 px-5 py-3">
      <div className="flex items-center gap-2">
        <label htmlFor="tamano-pagina" className="text-sm text-slate-500">
          Mostrar
        </label>
        <select
          id="tamano-pagina"
          value={tamanoPagina}
          onChange={(evento) => onTamanoPagina(Number(evento.target.value))}
          className="h-9 cursor-pointer rounded-lg border border-slate-300 bg-white px-2 text-sm font-semibold text-slate-800 outline-none focus:border-petroleo-500"
        >
          {opcionesTamano.map((opcion) => (
            <option key={opcion} value={opcion}>
              {opcion}
            </option>
          ))}
        </select>
        <span className="text-sm text-slate-500">por página</span>
      </div>

      <div className="min-w-0 flex-1" />

      {/* El rango concreto y no solo el total: "11 a 20 de 47" dice dónde estás,
          mientras que "47 registros" obliga a calcularlo. */}
      <span className="text-sm tabular-nums text-slate-500">
        {primero} a {ultimo} de {totalRegistros} registros
      </span>

      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={() => onPagina(pagina - 1)}
          disabled={pagina <= 1}
          className={BOTON}
        >
          <ChevronLeft size={17} aria-hidden="true" />
          <span className="sr-only">Página anterior</span>
        </button>

        <span
          className="min-w-[7.5rem] text-center text-sm font-semibold tabular-nums text-slate-700"
          aria-live="polite"
        >
          Página {pagina} de {totalPaginas}
        </span>

        <button
          type="button"
          onClick={() => onPagina(pagina + 1)}
          disabled={pagina >= totalPaginas}
          className={BOTON}
        >
          <ChevronRight size={17} aria-hidden="true" />
          <span className="sr-only">Página siguiente</span>
        </button>
      </div>
    </div>
  );
}
