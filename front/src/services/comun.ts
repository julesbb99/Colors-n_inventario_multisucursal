import axiosInstance from '../interceptors/axiosInstance';
import type { CrearUsuarioDto, SucursalDto, UnidadMedidaDto } from '../models/comun';
import type { Usuario } from '../models/auth';

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

/**
 * Usuarios de una sede, o de toda la red.
 *
 * Solo supervisión: la lista lleva correos y roles del personal, así que a un
 * operador la API le responde 403.
 */
export async function obtenerUsuarios(sucursalId: number | null): Promise<Usuario[]> {
  const { data } = await axiosInstance.get<Usuario[]>('/comun/usuarios', {
    params: paramsDeSede(sucursalId),
  });
  return data;
}

/**
 * Da de alta un usuario.
 *
 * La jerarquía la decide el SERVIDOR con el rol del token: el administrador crea
 * gerentes y operadores en cualquier sede, un gerente solo operadores de la
 * suya, y cualquier otra combinación devuelve 403. El frontend acota el
 * formulario para no ofrecer lo que va a fallar, pero no es él quien decide.
 */
export async function crearUsuario(peticion: CrearUsuarioDto) {
  const { data } = await axiosInstance.post('/comun/usuarios', peticion);
  return data;
}
