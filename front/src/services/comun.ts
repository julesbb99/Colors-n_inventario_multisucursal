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
export async function obtenerUsuarios(
  sucursalId: number | null,
  incluirInactivos = false,
): Promise<Usuario[]> {
  const { data } = await axiosInstance.get<Usuario[]>('/comun/usuarios', {
    params: {
      ...paramsDeSede(sucursalId),
      // Solo se manda cuando es true: un `incluirInactivos=false` explícito en
      // la URL es ruido, porque es justo lo que hace la API por defecto.
      ...(incluirInactivos ? { incluirInactivos: true } : {}),
    },
  });
  return data;
}

/**
 * Deshabilita un perfil: deja de poder iniciar sesión.
 *
 * NO BORRA NADA, aunque el verbo sea DELETE. El usuario sigue en la base con
 * toda su historia —sus ventas, sus movimientos, su rastro en la bitácora—
 * porque esas filas no pueden quedarse sin responsable.
 *
 * JERARQUÍA, la decide el SERVIDOR: el administrador deshabilita gerentes y
 * operadores de cualquier sede; un gerente solo operadores de SU sede. Nadie a
 * un administrador general, y nadie a sí mismo: las dos cosas dan 403.
 *
 * OJO: quien tenga sesión abierta sigue entrando hasta que su token caduque.
 */
export async function deshabilitarUsuario(id: number) {
  const { data } = await axiosInstance.delete(`/comun/usuarios/${id}`);
  return data;
}

/** Devuelve el acceso a un perfil deshabilitado. Misma jerarquía, en sentido contrario. */
export async function habilitarUsuario(id: number) {
  const { data } = await axiosInstance.post(`/comun/usuarios/${id}/habilitar`);
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
