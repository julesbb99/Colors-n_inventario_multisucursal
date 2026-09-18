import axiosInstance from '../interceptors/axiosInstance';
import { paramsDeSede } from './comun';
import type { ResumenGeneralDto } from '../models/dashboard';

/**
 * Tablero. Son CUATRO endpoints de solo lectura, no uno.
 *
 * NO EXISTE `/api/dashboard/kpis`: responde 404. Los bloques del tablero se
 * piden por separado, y cada uno acota por sede con el mismo parametro.
 *
 *   /resumen         totales de la pantalla de inicio
 *   /ventas          serie diaria y rankings de un periodo
 *   /inventario      stock por sede, lotes por vencer y ultimos movimientos
 *   /transferencias  traslados por estado y novedades recientes
 */
export async function obtenerResumenGeneral(
  sucursalId: number | null,
): Promise<ResumenGeneralDto> {
  const { data } = await axiosInstance.get<ResumenGeneralDto>('/dashboard/resumen', {
    params: paramsDeSede(sucursalId),
  });
  return data;
}
