import type { JwtPayload } from 'jwt-decode';

/**
 * Los roles TAL COMO VIAJAN EN EL TOKEN.
 *
 * El mismo rol se escribe de tres formas distintas en el sistema y conviene
 * tenerlas presentes:
 *
 *   ENUM de MySQL          'Administrador General'   (con espacios)
 *   enum del dominio C#     AdministradorGeneral
 *   claim del token         AdminGeneral             <- estas constantes
 *
 * Toda decision de permisos se toma con ESTAS, porque son las que la API
 * compara en sus politicas de autorizacion.
 */
export const ROLES = {
  adminGeneral: 'AdminGeneral',
  gerenteSucursal: 'GerenteSucursal',
  operador: 'Operador',
} as const;

export type Rol = (typeof ROLES)[keyof typeof ROLES];

/** Los roles que deciden, frente al que ejecuta. Espeja RolesColorsin.Supervision. */
export const ROLES_SUPERVISION: readonly Rol[] = [
  ROLES.adminGeneral,
  ROLES.gerenteSucursal,
];

/**
 * Claves de los claims dentro del JWT.
 *
 * SON LAS URI LARGAS, no los nombres cortos. La API firma con
 * JsonWebTokenHandler y `MapInboundClaims = false`, lo que significa que
 * escribe los tipos de `ClaimTypes` literalmente, sin traducirlos a 'nameid',
 * 'role' ni 'email'. Leer con los nombres cortos compila igual y devuelve
 * `undefined` en silencio: la sesion se pierde al recargar y nada avisa.
 */
export const CLAIMS = {
  nameIdentifier: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
  name: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
  email: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress',
  role: 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
  sucursalId: 'sucursal_id',
} as const;

/** Credenciales del formulario. El identificador es el CORREO: `usuarios` no tiene columna de usuario. */
export interface LoginRequest {
  email: string;
  password: string;
}

/** Usuario tal como lo expone la API. Nunca trae `passwordHash`. */
export interface Usuario {
  id: number;
  nombre: string;
  email: string;
  /**
   * Rol EN TEXTO DE BASE DE DATOS: 'Administrador General', 'Gerente de
   * Sucursal' u 'Operador'.
   *
   * SOLO PARA MOSTRAR. No coincide con las constantes de `ROLES` y por tanto no
   * sirve para decidir permisos; para eso esta el claim del token. Comparar
   * este campo contra 'AdminGeneral' no falla, simplemente nunca da verdadero.
   */
  rol: string;
  /** Nulo en el Administrador General, que no pertenece a ninguna sede. */
  sucursalId: number | null;
  sucursalNombre: string | null;
  /**
   * `false` si el perfil está deshabilitado: no puede iniciar sesión.
   *
   * NO ES UN BORRADO. El usuario sigue en la base con toda su historia —sus
   * ventas, sus movimientos, su rastro en la bitácora— porque esas filas no
   * pueden quedarse sin responsable. Se puede volver a habilitar.
   */
  activo: boolean;
}

/** Respuesta de POST /api/auth/login. */
export interface LoginResponse {
  token: string;
  /** Momento en que caduca, en UTC e ISO 8601. */
  expiracion: string;
  usuario: Usuario;
}

/**
 * El contenido del JWT una vez decodificado.
 *
 * Las claves van como literales y no con `CLAIMS.x` porque TypeScript no admite
 * nombres calculados en una interfaz. Si se cambia una, hay que cambiarla en los
 * dos sitios.
 */
export interface CustomJwtPayload extends JwtPayload {
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'?: string;
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'?: string;
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'?: string;
  /** Puede llegar como arreglo si algun dia un usuario tiene varios roles. */
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | string[];
  /** NO VIAJA SIEMPRE: el Administrador General no tiene sede y su token sale sin este claim. */
  sucursal_id?: string;
}

/**
 * La sesion ya resuelta: lo que el resto de la aplicacion consulta.
 *
 * Sale del TOKEN, no del cuerpo de la respuesta de login, por dos motivos: el
 * token es lo unico que sobrevive a una recarga de pagina, y su `role` es el
 * que la API compara de verdad.
 */
export interface Sesion {
  usuarioId: number;
  nombre: string;
  email: string;
  rol: Rol;
  /** Sede del token. `null` en el Administrador General, que significa TODAS, no ninguna. */
  sucursalId: number | null;
  /** Caducidad del token, del claim `exp`. */
  expiraEn: Date;
}
