import axiosInstance from '../interceptors/axiosInstance';
import type { SucursalDto, UnidadMedidaDto } from '../models/comun';

/**
 * `sucursalId` nulo NO significa "ninguna sede": significa "no filtres".
 *
 * Para el Administrador General eso es la red completa; a un gerente o a un
 * operador la API le acota igualmente a la suya, mande el parametro o no. Esta
 * funcion esta aqui y no repetida en cada servicio para que la regla se escriba
 * una sola vez.
 */
export function paramsDeSede(sucursalId: number | null): Record<string, number> {
  return sucursalId === null ? {} : { sucursalId };
}

export async function obtenerSucursales(): Promise<SucursalDto[]> {
  const { data } = await axiosInstance.get<SucursalDto[]>('/comun/sucursales');
  return data;
}

export async function obtenerUnidadesMedida(): Promise<UnidadMedidaDto[]> {
  const { data } = await axiosInstance.get<UnidadMedidaDto[]>('/comun/unidades-medida');
  return data;
}
