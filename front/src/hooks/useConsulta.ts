import { useCallback, useEffect, useState } from 'react';
import { esApiError, mensajeDeError } from '../models/api';

export interface EstadoConsulta<T> {
  datos: T;
  cargando: boolean;
  error: string | null;
  /** 403: el rol no alcanza, o la sede no es la del usuario. */
  esPermisos: boolean;
  recargar: () => void;
}

/**
 * Una lectura de la API con su estado de carga y de error.
 *
 * POR QUE EXISTE: las cinco pantallas repetian el mismo bloque de veinte lineas,
 * y con el se repetia la parte facil de olvidar, que es la BANDERA DE VIGENCIA.
 * Sin ella, cambiar de sede lanza una consulta nueva antes de que llegue la
 * anterior, y si la vieja responde despues pisa a la nueva: la pantalla termina
 * mostrando los datos de otra sede sin que nada falle.
 *
 * EL CONTRATO DE `deps`: quien llama lista todo lo que lee `ejecutar`. No hay
 * regla de lint que lo compruebe en este proyecto, asi que una dependencia
 * olvidada se nota como una pantalla que no se actualiza al cambiar el filtro.
 * El arreglo debe tener siempre el mismo tamaño entre renders.
 */
export function useConsulta<T>(
  ejecutar: () => Promise<T>,
  deps: readonly unknown[],
  valorInicial: T,
): EstadoConsulta<T> {
  const [datos, setDatos] = useState<T>(valorInicial);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [esPermisos, setEsPermisos] = useState(false);
  const [intento, setIntento] = useState(0);

  const recargar = useCallback(() => setIntento((n) => n + 1), []);

  useEffect(() => {
    let vigente = true;

    setCargando(true);
    setError(null);
    setEsPermisos(false);

    ejecutar()
      .then((respuesta) => {
        if (vigente) {
          setDatos(respuesta);
        }
      })
      .catch((fallo: unknown) => {
        if (!vigente) {
          return;
        }
        setError(mensajeDeError(fallo));
        setEsPermisos(esApiError(fallo) && fallo.esPermisos);
        // Se limpia lo que habia: dejar en pantalla los datos de la consulta
        // anterior junto a un mensaje de error hace creer que siguen vigentes.
        setDatos(valorInicial);
      })
      .finally(() => {
        if (vigente) {
          setCargando(false);
        }
      });

    return () => {
      vigente = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, intento]);

  return { datos, cargando, error, esPermisos, recargar };
}
