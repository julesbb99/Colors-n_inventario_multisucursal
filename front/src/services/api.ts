import axiosInstance from '../interceptors/axiosInstance';
import type { LoginRequest, LoginResponse } from '../models/auth';
import type { SucursalDto } from '../models/comun';
import type { ResumenGeneralDto } from '../models/dashboard';
import type { ExistenciaDto, LoteDto, ProductoDto } from '../models/inventario';

/**
 * Las llamadas a la API, en un solo sitio.
 *
 * POR QUE NO VAN SUELTAS EN CADA PANTALLA: la regla de aislamiento por sede se
 * expresa aqui una vez -`sucursalId` se manda solo cuando hay una sede elegida-
 * y no se repite en cada componente, donde olvidarla pasaria desapercibido.
 *
 * `sucursalId` nulo NO es "ninguna sede": es "no filtres". Para el Administrador
 * General eso significa la red completa; a un gerente o un operador la API le
 * acota igualmente a la suya, mande el parametro o no.
 */

/** Convierte una sede elegida en los parametros de consulta. */
function paramsDeSede(sucursalId: number | null): Record<string, number> {
  return sucursalId === null ? {} : { sucursalId };
}

export async function iniciarSesion(credenciales: LoginRequest): Promise<LoginResponse> {
  const { data } = await axiosInstance.post<LoginResponse>('/auth/login', credenciales);
  return data;
}

export async function obtenerResumenGeneral(
  sucursalId: number | null,
): Promise<ResumenGeneralDto> {
  const { data } = await axiosInstance.get<ResumenGeneralDto>('/dashboard/resumen', {
    params: paramsDeSede(sucursalId),
  });
  return data;
}

export async function obtenerExistencias(
  sucursalId: number | null,
): Promise<ExistenciaDto[]> {
  const { data } = await axiosInstance.get<ExistenciaDto[]>('/inventario/existencias', {
    params: paramsDeSede(sucursalId),
  });
  return data;
}

/**
 * Lotes con saldo que caducan dentro del umbral.
 *
 * `dias` omitido usa el umbral configurado en la API
 * (`AlertasInventario:DiasUmbralVencimiento`), que es el mismo con el que el
 * resumen cuenta sus alertas. Pasarle otro aqui haria que la tarjeta del panel y
 * la tabla de abajo contaran cosas distintas.
 *
 * Los lotes YA VENCIDOS con existencias entran por defecto y salen de primeros.
 */
export async function obtenerLotesProximosAVencer(
  sucursalId: number | null,
): Promise<LoteDto[]> {
  const { data } = await axiosInstance.get<LoteDto[]>(
    '/inventario/lotes/proximos-a-vencer',
    { params: paramsDeSede(sucursalId) },
  );
  return data;
}

export async function obtenerSucursales(): Promise<SucursalDto[]> {
  const { data } = await axiosInstance.get<SucursalDto[]>('/comun/sucursales');
  return data;
}

export async function obtenerProductos(): Promise<ProductoDto[]> {
  const { data } = await axiosInstance.get<ProductoDto[]>('/productos');
  return data;
}
