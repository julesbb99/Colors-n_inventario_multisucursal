/**
 * Un fallo de la API ya normalizado.
 *
 * El interceptor convierte TODO error -de red, de estado HTTP, de ProblemDetails-
 * en esta forma, para que ninguna pantalla tenga que hurgar en
 * `error.response.data.detail` ni distinguir un `AxiosError` de un `Error`
 * corriente. Las pantallas solo leen `mensaje` y, si les interesa, `estado`.
 */
export interface ApiError {
  /** Codigo HTTP, o 0 cuando la peticion ni siquiera llego a salir. */
  estado: number;
  /** Texto listo para mostrarle a una persona. */
  mensaje: string;
  /** 403: la operacion existe pero este rol o esta sede no la tiene permitida. */
  esPermisos: boolean;
  /** 429: segundos que pide la cabecera Retry-After antes de reintentar. */
  reintentarEnSegundos: number | null;
}

/** Discrimina un ApiError de cualquier otra cosa que caiga en un `catch`. */
export function esApiError(valor: unknown): valor is ApiError {
  return (
    typeof valor === 'object' &&
    valor !== null &&
    'estado' in valor &&
    'mensaje' in valor &&
    'esPermisos' in valor
  );
}

/** Saca un mensaje legible de lo que sea que haya llegado al `catch`. */
export function mensajeDeError(valor: unknown): string {
  if (esApiError(valor)) {
    return valor.mensaje;
  }
  if (valor instanceof Error) {
    return valor.message;
  }
  return 'Ocurrió un error inesperado.';
}
