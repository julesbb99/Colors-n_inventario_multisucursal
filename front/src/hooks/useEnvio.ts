import { useCallback, useRef, useState } from 'react';
import { esApiError, mensajeDeError } from '../models/api';

export interface EstadoEnvio {
  enviando: boolean;
  error: string | null;
  esPermisos: boolean;
  /** Ejecuta la acción cuidando el doble envío y traduciendo el fallo. */
  enviar: (accion: () => Promise<void>) => Promise<void>;
  limpiarError: () => void;
}

/**
 * El estado de un formulario mientras se envía.
 *
 * EL CANDADO ES UN REF, no el estado `enviando`. Entre el clic y el repintado
 * pasa un instante, y dos Enter seguidos entran los dos leyendo `enviando`
 * todavía en falso. En un formulario de venta eso son dos ventas idénticas con
 * stock descontado dos veces; el ref se actualiza en el acto y lo impide.
 */
export function useEnvio(): EstadoEnvio {
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [esPermisos, setEsPermisos] = useState(false);
  const enCurso = useRef(false);

  const limpiarError = useCallback(() => setError(null), []);

  const enviar = useCallback(async (accion: () => Promise<void>) => {
    if (enCurso.current) {
      return;
    }

    enCurso.current = true;
    setEnviando(true);
    setError(null);
    setEsPermisos(false);

    try {
      await accion();
    } catch (fallo: unknown) {
      setError(mensajeDeError(fallo));
      setEsPermisos(esApiError(fallo) && fallo.esPermisos);
    } finally {
      enCurso.current = false;
      setEnviando(false);
    }
  }, []);

  return { enviando, error, esPermisos, enviar, limpiarError };
}
