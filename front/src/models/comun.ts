/**
 * Sede de la red. Espeja SucursalDto de la API.
 *
 * OJO: no hay campo `codigo` ni marca de activa/inactiva. El esquema no tiene
 * esas columnas, asi que GET /api/comun/sucursales devuelve TODAS las sedes.
 */
export interface SucursalDto {
  id: number;
  nombre: string;
  ciudad: string;
  direccion: string | null;
  /** 'Matriz' o 'Sucursal'. */
  rolRed: string;
}

/**
 * El rol como lo espera la API AL CREAR un usuario: el ordinal del enum del
 * dominio, no el texto del claim ni el del ENUM de MySQL.
 *
 * Es la CUARTA grafía del mismo rol en este sistema, y conviene tenerlas juntas:
 *
 *   ordinal del dominio   1                       <- esto, solo al crear
 *   claim del token       'GerenteSucursal'       <- permisos
 *   ENUM de MySQL         'Gerente de Sucursal'   <- lo que devuelve UsuarioDto.rol
 *
 * Viaja como número porque la API no registra `JsonStringEnumConverter`.
 */
export const ROL_DOMINIO = {
  administradorGeneral: 0,
  gerenteDeSucursal: 1,
  operador: 2,
} as const;

export type ValorRolDominio = (typeof ROL_DOMINIO)[keyof typeof ROL_DOMINIO];

/**
 * Alta de usuario. El id de quien crea NO va: sale del token.
 *
 * `sucursalId` es obligatoria: gerentes y operadores trabajan siempre sobre una
 * sede, y la API rechaza el alta sin ella.
 */
export interface CrearUsuarioDto {
  nombre: string;
  email: string;
  /** En claro. Se guarda hasheada con BCrypt y nunca vuelve en la respuesta. */
  password: string;
  rol: ValorRolDominio;
  sucursalId: number;
}

/** Unidad de medida del catalogo. */
export interface UnidadMedidaDto {
  id: number;
  nombre: string;
  simbolo: string;
  /** Nulo en unidades que no son de volumen: sin el no hay conversion a litros. */
  factorConversionLitros: number | null;
}
