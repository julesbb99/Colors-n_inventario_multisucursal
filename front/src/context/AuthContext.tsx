import { createContext, useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { jwtDecode } from 'jwt-decode';
import { CLAVE_TOKEN, EVENTO_SESION_EXPIRADA } from '../interceptors/axiosInstance';
import { iniciarSesion } from '../services/api';
import { CLAIMS, ROLES } from '../models/auth';
import type { CustomJwtPayload, LoginRequest, Rol, Sesion } from '../models/auth';

export interface AuthContextValue {
  /** Quien esta conectado, o `null`. Sale del token, no del cuerpo del login. */
  sesion: Sesion | null;
  token: string | null;
  /** Rol en la grafia del claim ('AdminGeneral'...), la unica que la API compara. */
  rol: Rol | null;
  /** Sede del token. `null` en el Administrador General: significa TODAS, no ninguna. */
  sucursalId: number | null;
  estaAutenticado: boolean;
  esAdminGeneral: boolean;
  login: (credenciales: LoginRequest) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

/** Etiqueta legible de cada rol, para mostrar. No se usa para decidir permisos. */
export const ETIQUETA_ROL: Record<Rol, string> = {
  [ROLES.adminGeneral]: 'Administración general',
  [ROLES.gerenteSucursal]: 'Gerencia de sede',
  [ROLES.operador]: 'Operación',
};

/** Si el texto es uno de los tres roles conocidos. */
function esRolValido(valor: string): valor is Rol {
  return (Object.values(ROLES) as string[]).includes(valor);
}

/**
 * Reconstruye la sesion a partir del token.
 *
 * ES LA UNICA FUENTE DE VERDAD, y por eso basta guardar el token para sobrevivir
 * a una recarga: el nombre, el correo, el rol y la sede viajan dentro, firmados
 * por el servidor. Guardar ademas el objeto `usuario` del login abriria la puerta
 * a que las dos copias dijeran cosas distintas.
 *
 * Devuelve `null` si el token no se puede leer, si le falta el rol, si el rol no
 * es uno de los tres conocidos o si ya caduco.
 */
export function sesionDesdeToken(token: string): Sesion | null {
  let payload: CustomJwtPayload;

  try {
    payload = jwtDecode<CustomJwtPayload>(token);
  } catch {
    // Token corrupto o manipulado. No es un caso excepcional: basta con que
    // alguien edite localStorage a mano.
    return null;
  }

  // `exp` va en segundos desde 1970 en UTC. Esta comprobacion es solo para no
  // pintar una sesion que ya sabemos muerta; quien valida de verdad es la API
  // en cada peticion, que es lo unico que no se puede enganar desde el navegador.
  if (typeof payload.exp !== 'number' || payload.exp * 1000 <= Date.now()) {
    return null;
  }

  const rolCrudo = payload[CLAIMS.role];
  const rolTexto = Array.isArray(rolCrudo) ? rolCrudo[0] : rolCrudo;

  if (!rolTexto || !esRolValido(rolTexto)) {
    return null;
  }

  const usuarioId = Number(payload[CLAIMS.nameIdentifier]);
  if (!Number.isFinite(usuarioId)) {
    return null;
  }

  // El claim de sede NO viaja siempre: el Administrador General no pertenece a
  // ninguna. Su ausencia es informacion, no un dato faltante.
  const sedeCruda = payload[CLAIMS.sucursalId];
  const sucursalId = sedeCruda === undefined ? null : Number(sedeCruda);

  return {
    usuarioId,
    nombre: payload[CLAIMS.name] ?? '',
    email: payload[CLAIMS.email] ?? '',
    rol: rolTexto,
    sucursalId: sucursalId !== null && Number.isFinite(sucursalId) ? sucursalId : null,
    expiraEn: new Date(payload.exp * 1000),
  };
}

/** Lee el token guardado y lo descarta si ya no sirve. */
function restaurarSesion(): { token: string; sesion: Sesion } | null {
  const token = localStorage.getItem(CLAVE_TOKEN);
  if (!token) {
    return null;
  }

  const sesion = sesionDesdeToken(token);
  if (!sesion) {
    localStorage.removeItem(CLAVE_TOKEN);
    return null;
  }

  return { token, sesion };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  // Inicializador perezoso: leer localStorage es sincrono, asi que la sesion ya
  // esta lista en el primer render. Sin esto habria un instante con el usuario
  // en `null` y ProtectedRoute mandaria a /login antes de restaurar nada.
  const [estado, setEstado] = useState(() => restaurarSesion());

  const logout = useCallback(() => {
    localStorage.removeItem(CLAVE_TOKEN);
    setEstado(null);
  }, []);

  const login = useCallback(async (credenciales: LoginRequest) => {
    const respuesta = await iniciarSesion(credenciales);
    const sesion = sesionDesdeToken(respuesta.token);

    if (!sesion) {
      // La API respondio 200 con un token que no se puede usar. Es un fallo del
      // servidor, no de las credenciales, y conviene que se note.
      throw new Error(
        'La API devolvió un token que no se pudo leer. Revisa la configuración de JwtSettings.',
      );
    }

    localStorage.setItem(CLAVE_TOKEN, respuesta.token);
    setEstado({ token: respuesta.token, sesion });
  }, []);

  // El interceptor avisa cuando la API rechaza el token a mitad de una consulta.
  // Aqui solo se limpia el estado; de mandar a /login se encarga ProtectedRoute
  // al ver que ya no hay sesion.
  useEffect(() => {
    const alExpirar = () => setEstado(null);
    window.addEventListener(EVENTO_SESION_EXPIRADA, alExpirar);
    return () => window.removeEventListener(EVENTO_SESION_EXPIRADA, alExpirar);
  }, []);

  const valor = useMemo<AuthContextValue>(
    () => ({
      sesion: estado?.sesion ?? null,
      token: estado?.token ?? null,
      rol: estado?.sesion.rol ?? null,
      sucursalId: estado?.sesion.sucursalId ?? null,
      estaAutenticado: estado !== null,
      esAdminGeneral: estado?.sesion.rol === ROLES.adminGeneral,
      login,
      logout,
    }),
    [estado, login, logout],
  );

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}
