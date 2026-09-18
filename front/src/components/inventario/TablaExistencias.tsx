import { DataTable } from '../ui/DataTable';
import type { ColumnaTabla } from '../ui/DataTable';
import { StatusBadge } from '../ui/StatusBadge';
import type { EstadoBadge } from '../ui/StatusBadge';
import { ChipColor } from '../ui/ChipColor';
import type { ExistenciaDto } from '../../models/inventario';
import { formatearCOP, formatearVolumen } from '../../utils/formato';

/**
 * El estado visual de un saldo.
 *
 * La API solo manda `enAlerta` (`cantidadBase <= stockMinimo`). "Agotado" es un
 * matiz de la interfaz sobre ese mismo dato: un saldo en cero tambien esta en
 * alerta, pero no es lo mismo quedarse corto que no tener nada, y pintarlos
 * igual esconde el caso que hay que atender primero.
 */
function estadoDe(fila: ExistenciaDto): { texto: string; estado: EstadoBadge } {
  if (fila.cantidadBase <= 0) {
    return { texto: 'Agotado', estado: 'critico' };
  }
  if (fila.enAlerta) {
    return { texto: 'En alerta', estado: 'advertencia' };
  }
  return { texto: 'Con saldo', estado: 'activo' };
}

const COLUMNAS: ColumnaTabla<ExistenciaDto>[] = [
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
    render: (fila) => (
      <span className="font-semibold tabular-nums text-slate-900">
        {formatearVolumen(fila.cantidadBase, fila.unidadBaseSimbolo)}
      </span>
    ),
  },
  {
    id: 'minimo',
    header: 'Mínimo',
    align: 'derecha',
    render: (fila) => (
      <span className="tabular-nums text-slate-500">
        {formatearVolumen(fila.stockMinimo, fila.unidadBaseSimbolo)}
      </span>
    ),
  },
  {
    id: 'costo',
    header: 'Costo prom.',
    align: 'derecha',
    render: (fila) => (
      <span className="tabular-nums text-slate-600">{formatearCOP(fila.costoPromedio)}</span>
    ),
  },
  {
    id: 'estado',
    header: 'Estado',
    align: 'centro',
    render: (fila) => {
      const { texto, estado } = estadoDe(fila);
      return <StatusBadge estado={estado}>{texto}</StatusBadge>;
    },
  },
];

interface TablaExistenciasProps {
  existencias: ExistenciaDto[];
  cargando?: boolean;
}

export function TablaExistencias({ existencias, cargando = false }: TablaExistenciasProps) {
  return (
    <DataTable
      columnas={COLUMNAS}
      data={existencias}
      claveFila={(fila) => fila.id}
      cargando={cargando}
      estadoVacio="No hay existencias registradas para esta sede."
    />
  );
}
