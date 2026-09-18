import { useMemo } from 'react';
import { CalendarClock } from 'lucide-react';
import { DataTable } from '../ui/DataTable';
import type { ColumnaTabla } from '../ui/DataTable';
import { StatusBadge } from '../ui/StatusBadge';
import type { EstadoBadge } from '../ui/StatusBadge';
import { ChipColor } from '../ui/ChipColor';
import { estadoCaducidad } from '../../models/inventario';
import type { EstadoCaducidad, LoteDto } from '../../models/inventario';
import { formatearDias, formatearFechaSolo, formatearVolumen } from '../../utils/formato';

/** El estado de caducidad del dominio, traducido al vocabulario del badge. */
const ESTADO_BADGE: Record<EstadoCaducidad, EstadoBadge> = {
  Vigente: 'activo',
  'Por vencer': 'por_vencer',
  Vencido: 'vencido',
};

interface TablaAlertasVencimientoProps {
  lotes: LoteDto[];
  /** Umbral con el que la API contó las alertas. Clasificar con otro descuadraría la tarjeta. */
  diasUmbral: number;
  cargando?: boolean;
}

export function TablaAlertasVencimiento({
  lotes,
  diasUmbral,
  cargando = false,
}: TablaAlertasVencimientoProps) {
  // Las columnas dependen de `diasUmbral`, asi que se arman aqui y no fuera del
  // componente como en las tablas de umbral fijo.
  const columnas = useMemo<ColumnaTabla<LoteDto>[]>(
    () => [
      { id: 'lote', header: 'Lote', accessor: 'numeroLote' },
      {
        id: 'producto',
        header: 'Producto',
        render: (lote) => (
          <span className="flex items-center gap-2.5">
            <ChipColor nombre={lote.productoNombre} alto={26} />
            <span className="text-slate-700">{lote.productoNombre}</span>
          </span>
        ),
      },
      { id: 'sede', header: 'Sede', accessor: 'sucursalNombre' },
      {
        id: 'vence',
        header: 'Vence',
        align: 'derecha',
        render: (lote) => (
          <span className="tabular-nums text-slate-600">
            {formatearFechaSolo(lote.fechaVencimiento)}
          </span>
        ),
      },
      {
        id: 'dias',
        header: 'Días',
        align: 'derecha',
        render: (lote) => (
          <span
            className={`font-semibold tabular-nums ${
              lote.vencido ? 'text-terracota-700' : 'text-slate-700'
            }`}
          >
            {formatearDias(lote.diasParaVencer)}
          </span>
        ),
      },
      {
        id: 'saldo',
        header: 'Saldo',
        align: 'derecha',
        render: (lote) => (
          <span className="font-semibold tabular-nums text-slate-900">
            {formatearVolumen(lote.cantidadBase, lote.unidadBaseSimbolo)}
          </span>
        ),
      },
      {
        id: 'estado',
        header: 'Estado',
        align: 'centro',
        render: (lote) => {
          const estado = estadoCaducidad(lote, diasUmbral);
          return <StatusBadge estado={ESTADO_BADGE[estado]}>{estado}</StatusBadge>;
        },
      },
    ],
    [diasUmbral],
  );

  return (
    <section className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <div className="min-w-0 flex-1">
          <h2 className="text-base font-semibold text-slate-900">Alertas de caducidad</h2>
          <p className="mt-0.5 text-sm text-slate-500">
            Lotes con existencias, en orden FEFO: primero el que vence antes
          </p>
        </div>

        {/*
          El umbral se muestra porque sin él la cifra no se puede leer: "3 lotes
          por vencer" no significa lo mismo a 30 días que a 180. El número lo
          manda la API en el propio resumen.
        */}
        <span className="inline-flex items-center gap-1.5 rounded-full bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-600">
          <CalendarClock size={14} aria-hidden="true" />
          Umbral {diasUmbral} días
        </span>
      </div>

      <DataTable
        columnas={columnas}
        data={lotes}
        claveFila={(lote) => lote.id}
        cargando={cargando}
        estadoVacio="Ningún lote con existencias caduca dentro del umbral. Los lotes ya vencidos también aparecerán aquí cuando los haya."
      />
    </section>
  );
}
