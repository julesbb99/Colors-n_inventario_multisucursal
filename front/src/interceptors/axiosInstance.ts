import axios from 'axios';
import type { AxiosError, InternalAxiosRequestConfig } from 'axios';
import type { ApiError } from '../models/api';

/** Clave del token en localStorage. Un solo sitio la escribe y la lee. */
export const CLAVE_TOKEN = 'colorsin_token';

/**
 * Evento que se emite cuando la API rechaza el token.
 *
 * POR QUE UN EVENTO Y NO `window.location = '/login'`. El interceptor vive fuera
 * de React y no puede usar `useNavigate`. Redirigir con `window.location`
 * funcionaria, pero recarga la aplicacion entera y deja el estado de React
 * hablando de un usuario que ya no existe durante el instante previo. Con el
 * evento, AuthContext se entera, limpia su estado y ProtectedRoute manda a
 * /login por el camino normal del enrutador.
 */
export const EVENTO_SESION_EXPIRADA = 'colorsin:sesion-expirada';

/** Forma de un ProblemDetails de ASP.NET Core. */
interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
}

const axiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
  headers: { 'Content-Type': 'application/json' },
  // Sin esto una API caida deja la pantalla girando para siempre.
  timeout: 20000,
});

// -----------------------------------------------------------------------------
// Peticion: firma cada llamada con el token guardado
// -----------------------------------------------------------------------------
axiosInstance.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = localStorage.getItem(CLAVE_TOKEN);

  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }

  return config;
});

// -----------------------------------------------------------------------------
// Respuesta: traduce cada fallo a un ApiError
// -----------------------------------------------------------------------------

/** Si la peticion que fallo era el propio inicio de sesion. */
function esLogin(config: InternalAxiosRequestConfig | undefined): boolean {
  return (config?.url ?? '').includes('/auth/login');
}

/** El mensaje que escribio el servidor, o uno por defecto. */
function mensajeDelServidor(error: AxiosError<ProblemDetails>, porDefecto: string): string {
  const cuerpo = error.response?.data;
  return cuerpo?.detail ?? cuerpo?.title ?? porDefecto;
}

/** Segundos de la cabecera Retry-After, o `null` si no vino o no es un numero. */
function leerRetryAfter(error: AxiosError): number | null {
  const cabecera = error.response?.headers?.['retry-after'];

  if (typeof cabecera !== 'string' && typeof cabecera !== 'number') {
    return null;
  }

  const segundos = Number(cabecera);
  return Number.isFinite(segundos) && segundos > 0 ? segundos : null;
}

axiosInstance.interceptors.response.use(
  (respuesta) => respuesta,
  (error: AxiosError<ProblemDetails>): Promise<never> => {
    // La peticion no llego a salir: no hay red, la API esta apagada o se agoto
    // el tiempo. Es el fallo que mas confunde en desarrollo, asi que el mensaje
    // dice donde mirar.
    if (!error.response) {
      const fallo: ApiError = {
        estado: 0,
        mensaje:
          'No se pudo contactar la API. Verifica que este levantada en ' +
          `${import.meta.env.VITE_API_URL} y que el origen este permitido en CORS.`,
        esPermisos: false,
        reintentarEnSegundos: null,
      };
      return Promise.reject(fallo);
    }

    const estado = error.response.status;

    // --- 401 ---------------------------------------------------------------
    if (estado === 401) {
      // EXCEPTO EN EL LOGIN. Ahi un 401 significa "credenciales incorrectas",
      // no "tu sesion expiro": borrar el almacenamiento y mandar a /login desde
      // la propia pantalla de /login produciria un parpadeo y borraria el
      // mensaje de error que la persona necesita leer.
      if (esLogin(error.config)) {
        const fallo: ApiError = {
          estado,
          mensaje: mensajeDelServidor(error, 'El correo o la contrasena no son correctos.'),
          esPermisos: false,
          reintentarEnSegundos: null,
        };
        return Promise.reject(fallo);
      }

      localStorage.removeItem(CLAVE_TOKEN);
      window.dispatchEvent(new CustomEvent(EVENTO_SESION_EXPIRADA));

      const fallo: ApiError = {
        estado,
        mensaje: 'Tu sesion expiro. Vuelve a iniciar sesion.',
        esPermisos: false,
        reintentarEnSegundos: null,
      };
      return Promise.reject(fallo);
    }

    // --- 403 ---------------------------------------------------------------
    // Hay DOS reglas distintas detras de este codigo y conviene nombrarlas, aunque
    // la API responda igual en las dos: el rol no alcanza para la operacion, o la
    // sede no es la tuya. La sesion sigue siendo valida, asi que NO se limpia nada.
    if (estado === 403) {
      const fallo: ApiError = {
        estado,
        mensaje: mensajeDelServidor(
          error,
          'No tienes permiso para esta operacion. Puede ser por tu rol o porque ' +
            'la sede consultada no es la tuya.',
        ),
        esPermisos: true,
        reintentarEnSegundos: null,
      };
      return Promise.reject(fallo);
    }

    // --- 429 ---------------------------------------------------------------
    // Solo lo devuelve POST /api/auth/login, que tiene limite de 5 intentos por
    // minuto y por IP.
    if (estado === 429) {
      const segundos = leerRetryAfter(error);
      const espera =
        segundos === null
          ? 'Espera un momento y vuelve a intentarlo.'
          : `Espera ${segundos} segundos y vuelve a intentarlo.`;

      const fallo: ApiError = {
        estado,
        mensaje: `Demasiados intentos. ${espera}`,
        esPermisos: false,
        reintentarEnSegundos: segundos,
      };
      return Promise.reject(fallo);
    }

    // --- El resto ----------------------------------------------------------
    const fallo: ApiError = {
      estado,
      mensaje: mensajeDelServidor(error, `La API respondio ${estado}.`),
      esPermisos: false,
      reintentarEnSegundos: null,
    };
    return Promise.reject(fallo);
  },
);

export default axiosInstance;
