import { CalendarClock } from 'lucide-react';
import { BadgeCaducidad } from '../ui/Badge';
import { ChipColor } from '../ui/ChipColor';
import { estadoCaducidad } from '../../models/inventario';
import type { LoteDto } from '../../models/inventario';
import { formatearDias, formatearFechaSolo, formatearVolumen } from '../../utils/formato';

interface TablaAlertasVencimientoProps {
  lotes: LoteDto[];
  /** Umbral con el que la API conto las alertas. Clasificar con otro descuadraria la tarjeta. */
  diasUmbral: number;
}

export function TablaAlertasVencimiento({ lotes, diasUmbral }: TablaAlertasVencimientoProps) {
  return (
    <section className="overflow-hidden rounded-xl border border-slate-200 bg-white">
      <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 px-5 py-4">
        <div className="min-w-0 flex-1">
          <h2 className="text-base font-semibold text-slate-900">Alertas de caducidad</h2>
          <p className="mt-0.5 text-sm text-slate-500">
            Lotes con existencias, en orden FEFO: primero el que vence antes
          </p>
        </div>

        {/*
          El umbral se muestra porque sin el la cifra no se puede leer: "3 lotes
          por vencer" no significa lo mismo a 30 dias que a 180. El numero lo
          manda la API en el propio resumen.
        */}
        <span className="inline-flex items-center gap-1.5 rounded-full bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-600">
          <CalendarClock size={14} aria-hidden="true" />
          Umbral {diasUmbral} dias
        </span>
      </div>

      {lotes.length === 0 ? (
        <p className="px-5 py-10 text-center text-sm text-slate-500">
          Ningun lote con existencias caduca dentro del umbral. Los lotes ya vencidos tambien
          apareceran aqui cuando los haya.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200 bg-slate-50 text-left text-[11px] uppercase tracking-wider text-slate-500">
                <th scope="col" className="px-5 py-3 font-semibold">
                  Lote
                </th>
                <th scope="col" className="px-5 py-3 font-semibold">
                  Producto
                </th>
                <th scope="col" className="px-5 py-3 font-semibold">
                  Sede
                </th>
                <th scope="col" className="px-5 py-3 text-right font-semibold">
                  Vence
                </th>
                <th scope="col" className="px-5 py-3 text-right font-semibold">
                  Dias
                </th>
                <th scope="col" className="px-5 py-3 text-right font-semibold">
                  Saldo
                </th>
                <th scope="col" className="px-5 py-3 text-center font-semibold">
                  Estado
                </th>
              </tr>
            </thead>
            <tbody>
              {lotes.map((lote) => {
                const estado = estadoCaducidad(lote, diasUmbral);

                return (
                  <tr key={lote.id} className="border-b border-slate-100 last:border-0">
                    <td className="whitespace-nowrap px-5 py-3 font-semibold text-slate-900">
                      {lote.numeroLote}
                    </td>
                    <td className="px-5 py-3">
                      <span className="flex items-center gap-2.5">
                        <ChipColor nombre={lote.productoNombre} alto={26} />
                        <span className="text-slate-700">{lote.productoNombre}</span>
                      </span>
                    </td>
                    <td className="whitespace-nowrap px-5 py-3 text-slate-600">
                      {lote.sucursalNombre}
                    </td>
                    <td className="whitespace-nowrap px-5 py-3 text-right tabular-nums text-slate-600">
                      {formatearFechaSolo(lote.fechaVencimiento)}
                    </td>
                    <td
                      className={`whitespace-nowrap px-5 py-3 text-right font-semibold tabular-nums ${
                        lote.vencido ? 'text-red-700' : 'text-slate-700'
                      }`}
                    >
                      {formatearDias(lote.diasParaVencer)}
                    </td>
                    <td className="whitespace-nowrap px-5 py-3 text-right font-semibold tabular-nums text-slate-900">
                      {formatearVolumen(lote.cantidadBase, lote.unidadBaseSimbolo)}
                    </td>
                    <td className="px-5 py-3 text-center">
                      <BadgeCaducidad estado={estado} />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
