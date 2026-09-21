import axiosInstance from '../interceptors/axiosInstance';
import { paramsDeSede } from './comun';
import type {
  ComparativaSucursalesDto,
  HistoricoVentasDto,
  ResumenGeneralDto,
  RotacionProductosDto,
} from '../models/dashboard';

/**
 * Tablero. Son SIETE endpoints de solo lectura, no uno.
 *
 * NO EXISTE `/api/dashboard/kpis`: responde 404. Los bloques del tablero se
 * piden por separado, y cada uno acota por sede con el mismo parametro.
 *
 *   /resumen                 totales de la pantalla de inicio
 *   /ventas                  serie diaria y rankings de un periodo
 *   /ventas/historico        mes a mes y acumulado de toda la historia
 *   /rotacion                alta y baja demanda del catalogo
 *   /sucursales/comparativa  rendimiento comparado. SOLO supervision.
 *   /inventario              stock por sede, lotes por vencer y movimientos
 *   /transferencias          traslados por estado y novedades recientes
 */
export async function obtenerResumenGeneral(
  sucursalId: number | null,
): Promise<ResumenGeneralDto> {
  const { data } = await axiosInstance.get<ResumenGeneralDto>('/dashboard/resumen', {
    params: paramsDeSede(sucursalId),
  });
  return data;
}

/**
 * Ventas mes a mes y el acumulado de todas las ventas realizadas.
 *
 * `meses` cuenta el actual: 12 son once hacia atrás más este. NO afecta al
 * acumulado, que es de toda la historia; pedir menos meses no esconde ninguna
 * venta del total.
 */
export async function obtenerHistoricoVentas(
  sucursalId: number | null,
  meses = 12,
): Promise<HistoricoVentasDto> {
  const { data } = await axiosInstance.get<HistoricoVentasDto>('/dashboard/ventas/historico', {
    params: { ...paramsDeSede(sucursalId), meses },
  });
  return data;
}

/**
 * Rotación del catálogo: qué se mueve rápido y qué lleva meses quieto.
 *
 * Sin fechas, la API toma los últimos 90 días. Noventa y no treinta porque la
 * rotación de un producto de pintura no se ve en un mes: con pocas ventas
 * mensuales, casi todo el catálogo saldría «sin movimiento».
 */
export async function obtenerRotacionProductos(
  sucursalId: number | null,
  desde?: string | null,
  hasta?: string | null,
): Promise<RotacionProductosDto> {
  const { data } = await axiosInstance.get<RotacionProductosDto>('/dashboard/rotacion', {
    params: {
      ...paramsDeSede(sucursalId),
      ...(desde ? { desde } : {}),
      ...(hasta ? { hasta } : {}),
    },
  });
  return data;
}

/**
 * Comparativa de rendimiento entre sedes.
 *
 * NO LLEVA `sucursalId`, y no es un olvido: devuelve TODAS las sedes a
 * propósito, porque una comparativa de una sola fila no compara nada. La API
 * responde 403 al operario.
 */
export async function obtenerComparativaSucursales(
  desde?: string | null,
  hasta?: string | null,
): Promise<ComparativaSucursalesDto> {
  const { data } = await axiosInstance.get<ComparativaSucursalesDto>(
    '/dashboard/sucursales/comparativa',
    { params: { ...(desde ? { desde } : {}), ...(hasta ? { hasta } : {}) } },
  );
  return data;
}
