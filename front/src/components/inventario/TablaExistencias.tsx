import { Badge } from '../ui/Badge';
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
function estadoDe(fila: ExistenciaDto): { texto: string; tono: 'verde' | 'amarillo' | 'rojo' } {
  if (fila.cantidadBase <= 0) {
    return { texto: 'Agotado', tono: 'rojo' };
  }
  if (fila.enAlerta) {
    return { texto: 'En alerta', tono: 'amarillo' };
  }
  return { texto: 'Con saldo', tono: 'verde' };
}

export function TablaExistencias({ existencias }: { existencias: ExistenciaDto[] }) {
  if (existencias.length === 0) {
    return (
      <div className="rounded-xl border border-slate-200 bg-white px-5 py-12 text-center">
        <p className="text-sm text-slate-500">
          No hay existencias registradas para esta sede.
        </p>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50 text-left text-[11px] uppercase tracking-wider text-slate-500">
              <th scope="col" className="px-5 py-3 font-semibold">
                Producto
              </th>
              <th scope="col" className="px-5 py-3 font-semibold">
                Sede
              </th>
              <th scope="col" className="px-5 py-3 text-right font-semibold">
                Existencias
              </th>
              <th scope="col" className="px-5 py-3 text-right font-semibold">
                Minimo
              </th>
              <th scope="col" className="px-5 py-3 text-right font-semibold">
                Costo prom.
              </th>
              <th scope="col" className="px-5 py-3 text-center font-semibold">
                Estado
              </th>
            </tr>
          </thead>
          <tbody>
            {existencias.map((fila) => {
              const estado = estadoDe(fila);

              return (
                <tr key={fila.id} className="border-b border-slate-100 last:border-0">
                  <td className="px-5 py-3.5">
                    <span className="flex items-center gap-3">
                      <ChipColor nombre={fila.productoNombre} />
                      <span className="font-medium text-slate-900">{fila.productoNombre}</span>
                    </span>
                  </td>
                  <td className="whitespace-nowrap px-5 py-3.5 text-slate-600">
                    {fila.sucursalNombre}
                  </td>
                  <td className="whitespace-nowrap px-5 py-3.5 text-right font-semibold tabular-nums text-slate-900">
                    {formatearVolumen(fila.cantidadBase, fila.unidadBaseSimbolo)}
                  </td>
                  <td className="whitespace-nowrap px-5 py-3.5 text-right tabular-nums text-slate-500">
                    {formatearVolumen(fila.stockMinimo, fila.unidadBaseSimbolo)}
                  </td>
                  <td className="whitespace-nowrap px-5 py-3.5 text-right tabular-nums text-slate-600">
                    {formatearCOP(fila.costoPromedio)}
                  </td>
                  <td className="px-5 py-3.5 text-center">
                    <Badge tono={estado.tono}>{estado.texto}</Badge>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
