import { useMemo } from 'react';
import { Pencil, RotateCcw, Trash2 } from 'lucide-react';
import { DataTable } from '../ui/DataTable';
import type { ColumnaTabla } from '../ui/DataTable';
import { StatusBadge } from '../ui/StatusBadge';
import type { EstadoBadge } from '../ui/StatusBadge';
import { ChipColor } from '../ui/ChipColor';
import { useCatalogos } from '../../hooks/useCatalogos';
import { estadoExistencia } from '../../models/inventario';
import type { EstadoExistencia, ExistenciaDto } from '../../models/inventario';
import { convertirCantidad, convertirCosto, factorPorSimbolo } from '../../utils/unidades';
import { formatearCOP, formatearVolumen } from '../../utils/formato';

/** Cómo se pinta cada estado. El estado lo decide `estadoExistencia`, no esta tabla. */
const INSIGNIA: Record<EstadoExistencia, { texto: string; estado: EstadoBadge }> = {
  deshabilitado: { texto: 'Deshabilitado', estado: 'inactivo' },
  agotado: { texto: 'Agotado', estado: 'critico' },
  alerta: { texto: 'En alerta', estado: 'advertencia' },
  conSaldo: { texto: 'Con saldo', estado: 'activo' },
};

interface TablaExistenciasProps {
  existencias: ExistenciaDto[];
  cargando?: boolean;
  /**
   * Unidad en la que mostrar cantidades y costo. Solo afecta a lo que se ve.
   *
   * Nula, o de una unidad que no sea de volumen, deja cada fila en su propia
   * unidad base.
   */
  unidadDestinoId?: number | null;
  /**
   * Si quien mira puede MODIFICAR esa fila concreta.
   *
   * Se decide por fila y no por pantalla porque ahora se ven las existencias de
   * todas las sedes pero solo se tocan las de la propia. NO ES EL CONTROL: la
   * API comprueba lo mismo y responde 403. Esconder los botones evita ofrecer
   * algo que va a fallar.
   */
  puedeEditar?: (fila: ExistenciaDto) => boolean;
  onEditar?: (fila: ExistenciaDto) => void;
  onDesactivar?: (fila: ExistenciaDto) => void;
  onReactivar?: (fila: ExistenciaDto) => void;
  /** Id de la fila con una acción en vuelo: deshabilita sus botones. */
  filaOcupada?: number | null;
  /** Lo manda la pantalla porque depende de la pestaña, no de la tabla. */
  estadoVacio?: string;
}

export function TablaExistencias({
  existencias,
  cargando = false,
  unidadDestinoId = null,
  puedeEditar,
  onEditar,
  onDesactivar,
  onReactivar,
  filaOcupada = null,
  estadoVacio = 'No hay existencias registradas para esta sede.',
}: TablaExistenciasProps) {
  const { unidades } = useCatalogos();

  const destino = useMemo(
    () => unidades.find((unidad) => unidad.id === unidadDestinoId) ?? null,
    [unidades, unidadDestinoId],
  );

  const columnas = useMemo<ColumnaTabla<ExistenciaDto>[]>(() => {
    const factorDestino = destino?.factorConversionLitros ?? null;

    /**
     * Resuelve con qué número y qué símbolo se pinta una fila.
     *
     * Cada producto tiene SU unidad base, así que la decisión es por fila y no
     * global: si mañana entra uno que se lleva por piezas, su factor es nulo,
     * no se convierte y sigue mostrando su propia unidad en vez de una cifra
     * inventada en galones.
     */
    function convertir(
      valor: number,
      simboloBase: string | null,
      transformar: (valor: number, factorBase: number, factorDestino: number) => number,
    ): { valor: number; simbolo: string | null } {
      const factorBase = factorPorSimbolo(unidades, simboloBase);

      if (destino === null || factorDestino === null || factorBase === null) {
        return { valor, simbolo: simboloBase };
      }

      return {
        valor: transformar(valor, factorBase, factorDestino),
        simbolo: destino.simbolo,
      };
    }

    return [
      {
        id: 'producto',
        header: 'Producto',
        render: (fila) => (
          <span className="flex items-center gap-3">
            <ChipColor nombre={fila.productoNombre} />
            <span className="font-medium text-slate-900">{fila.productoNombre}</span>
          </span>
        ),
      },
      { id: 'sede', header: 'Sede', accessor: 'sucursalNombre' },
      {
        id: 'existencias',
        header: 'Existencias',
        align: 'derecha',
        render: (fila) => {
          const { valor, simbolo } = convertir(
            fila.cantidadBase,
            fila.unidadBaseSimbolo,
            convertirCantidad,
          );
          return (
            <span className="font-semibold tabular-nums text-slate-900">
              {formatearVolumen(valor, simbolo)}
            </span>
          );
        },
      },
      {
        id: 'minimo',
        header: 'Mínimo',
        align: 'derecha',
        render: (fila) => {
          const { valor, simbolo } = convertir(
            fila.stockMinimo,
            fila.unidadBaseSimbolo,
            convertirCantidad,
          );
          return (
            <span className="tabular-nums text-slate-500">
              {formatearVolumen(valor, simbolo)}
            </span>
          );
        },
      },
      {
        id: 'costo',
        header: 'Costo prom.',
        align: 'derecha',
        render: (fila) => {
          // El símbolo va en la CELDA y no en el encabezado: cada fila puede
          // tener su unidad base, así que un solo rótulo arriba mentiría en
          // cuanto entre un producto que no sea de volumen.
          const { valor, simbolo } = convertir(
            fila.costoPromedio,
            fila.unidadBaseSimbolo,
            convertirCosto,
          );
          return (
            <span className="whitespace-nowrap tabular-nums text-slate-600">
              {formatearCOP(valor)}
              {simbolo ? <span className="text-slate-400"> / {simbolo}</span> : null}
            </span>
          );
        },
      },
      {
        id: 'estado',
        header: 'Estado',
        align: 'centro',
        render: (fila) => {
          const { texto, estado } = INSIGNIA[estadoExistencia(fila)];
          return <StatusBadge estado={estado}>{texto}</StatusBadge>;
        },
      },
      {
        id: 'acciones',
        header: '',
        align: 'derecha',
        ancho: 'w-28',
        render: (fila) => {
          // La columna existe siempre, aunque quede vacía en las filas de otras
          // sedes: si apareciera y desapareciera según la fila, el ancho de la
          // tabla bailaría al cambiar de pestaña o de sede.
          if (puedeEditar === undefined || !puedeEditar(fila)) {
            return null;
          }

          const ocupada = filaOcupada === fila.id;

          return (
            <span className="flex items-center justify-end gap-1">
              {fila.activo ? (
                <>
                  <button
                    type="button"
                    onClick={() => onEditar?.(fila)}
                    disabled={ocupada}
                    title="Cambiar el mínimo de reposición"
                    aria-label={`Editar ${fila.productoNombre} en ${fila.sucursalNombre}`}
                    className="rounded-md p-1.5 text-slate-500 transition hover:bg-slate-100 hover:text-slate-800 disabled:cursor-not-allowed disabled:opacity-40"
                  >
                    <Pencil size={16} aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    onClick={() => onDesactivar?.(fila)}
                    disabled={ocupada}
                    title="Deshabilitar en esta sede. No borra nada."
                    aria-label={`Deshabilitar ${fila.productoNombre} en ${fila.sucursalNombre}`}
                    className="rounded-md p-1.5 text-slate-500 transition hover:bg-terracota-50 hover:text-terracota-700 disabled:cursor-not-allowed disabled:opacity-40"
                  >
                    <Trash2 size={16} aria-hidden="true" />
                  </button>
                </>
              ) : (
                <button
                  type="button"
                  onClick={() => onReactivar?.(fila)}
                  disabled={ocupada}
                  title="Volver a habilitar en esta sede"
                  aria-label={`Habilitar de nuevo ${fila.productoNombre} en ${fila.sucursalNombre}`}
                  className="flex items-center gap-1.5 rounded-md px-2 py-1.5 text-xs font-semibold text-petroleo-700 transition hover:bg-petroleo-50 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  <RotateCcw size={15} aria-hidden="true" />
                  Habilitar
                </button>
              )}
            </span>
          );
        },
      },
    ];
  }, [unidades, destino, puedeEditar, onEditar, onDesactivar, onReactivar, filaOcupada]);

  return (
    <DataTable
      columnas={columnas}
      data={existencias}
      claveFila={(fila) => fila.id}
      cargando={cargando}
      estadoVacio={estadoVacio}
    />
  );
}
