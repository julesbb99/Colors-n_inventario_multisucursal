import { useContext } from 'react';
import { SedeContext } from '../context/SedeContext';
import type { SedeContextValue } from '../context/SedeContext';

/** La sede por la que se esta mirando. Lanza si se usa fuera de `SedeProvider`. */
export function useSede(): SedeContextValue {
  const contexto = useContext(SedeContext);

  if (contexto === undefined) {
    throw new Error('useSede se uso fuera de <SedeProvider>.');
  }

  return contexto;
}
